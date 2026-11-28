using Application.Persistence;
using Application.UseCases.EventBooking;
using Application.UseCases.EventStages;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// EVENT-TERMS: an event keeps the policy it was signed with (EventTerms). A policy saved later applies to new contracts only:
/// the lock date, supplier deadline, decrease floor and settlement due date of signed events do not move.
/// </summary>
public sealed class EventTermsTests : IDisposable
{
    private const int CustomerId = 1, ManagerId = 2;
    private static DateOnly Today => VietnamTime.GetVietnamToday();

    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly TestFixtures.RecordingEmailOutbox _outbox = new();
    private readonly PasswordService _passwords = new(new PasswordHasher<User>());

    public EventTermsTests()
    {
        _db.Users.AddRange(TestFixtures.NewUser(CustomerId, "Customer", "khach@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.AddRange(
            TestFixtures.NewCatalogItem(10, "Thuê sảnh", "Hall", "Gói", 30_000_000m),
            TestFixtures.NewCatalogItem(11, "Thực đơn", "Catering", "Bàn", 3_500_000m));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private EventMessages Messages() => new(_db, _outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });
    private BookEvent Book() => new(new EventBookingStore(_db), _passwords, Messages(), new UnitOfWork(_db), new ProjectRepository(_db, new ProjectReadModel(_db)));
    private Task<int> LockDue() => new LockDueEvents(new EventStageStore(_db), Messages(), new UnitOfWork(_db)).HandleAsync(Today);

    private static CreateProjectCommand Signing(int daysAway = 40, string? stamp = null) =>
        new("Ánh & Minh", "anh@test.vn", "0912345678", "Sảnh Hoàng Gia", "tối", Today.AddDays(daysAway), 0, 0, "Cash", null,
            [new(10, "", 1, 30_000_000m), new(11, "", 20, 3_500_000m)], stamp);

    private void SavePolicy(string key, decimal value, DateOnly from)
    {
        _db.PolicyConfigs.Add(PolicyConfig.Create(key, value, from, ManagerId, DateTime.UtcNow));
        _db.SaveChanges();
    }

    private WeddingProject SeedEvent(int id, int daysAway, EventPolicy? terms, string status = EventStatus.Adjusting)
    {
        var project = TestFixtures.NewProject(id, $"WPSM-2026-{id:000}", CustomerId, ManagerId, eventDate: Today.AddDays(daysAway), status: status);
        if (terms is not null) project.Terms = EventTerms.Capture(terms, Today.AddDays(-60), DateTime.UtcNow);
        _db.WeddingProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    // ── Rules ───────────────────────────────────────────────────────────────

    [Fact]
    public void Terms_copy_every_value_of_the_policy()
    {
        var policy = new EventPolicy(25m, 20m, 20, 120, 12, 9, 5);
        var terms = EventTerms.Capture(policy, new DateOnly(2026, 12, 1), DateTime.UtcNow);
        Assert.Equal(policy, terms.ToPolicy());
        Assert.Equal(new DateOnly(2026, 12, 1), terms.PolicyEffectiveFrom);
    }

    [Fact]
    public void An_event_without_terms_falls_back_to_the_policy_in_force()
    {
        var inForce = new EventPolicy(LockDays: 14);
        Assert.Equal(inForce, EventTerms.PolicyOf(null, inForce));
        Assert.Equal(10, EventTerms.PolicyOf(EventTerms.Capture(EventPolicy.Default, null, DateTime.UtcNow), inForce).LockDays);
    }

    [Fact]
    public void The_stamp_changes_only_when_a_value_changes()
    {
        Assert.Equal(new EventPolicy(DepositRatio: 30.00m).Stamp(), EventPolicy.Default.Stamp());
        Assert.NotEqual(new EventPolicy(LockDays: 14).Stamp(), EventPolicy.Default.Stamp());
    }

    [Fact]
    public void Signing_stops_when_the_policy_changed_after_the_form_was_opened()
    {
        EventBookingPolicy.EnsurePolicyUnchanged(null, EventPolicy.Default);                       // older clients: not checked
        EventBookingPolicy.EnsurePolicyUnchanged(EventPolicy.Default.Stamp(), EventPolicy.Default);
        var error = Assert.Throws<BusinessRuleException>(() => EventBookingPolicy.EnsurePolicyUnchanged(EventPolicy.Default.Stamp(), new EventPolicy(LockDays: 14)));
        Assert.Contains("vừa được thay đổi", error.Message);
    }

    // ── Signing ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Signing_saves_the_policy_in_force_with_its_effective_date()
    {
        SavePolicy(EventPolicy.Keys.LockDays, 12, Today.AddDays(-5));
        SavePolicy(EventPolicy.Keys.DepositRatio, 40, Today.AddDays(-2));
        SavePolicy(EventPolicy.Keys.LockDays, 20, Today.AddDays(10));   // not in force yet

        var created = await Book().HandleAsync(Signing(), ManagerId);

        var terms = _db.EventTerms.AsNoTracking().Single();
        Assert.Equal((12, 40m, 8, Today.AddDays(-2)), (terms.LockDays, terms.DepositRatio, terms.SupplierDeadlineDays, terms.PolicyEffectiveFrom));
        Assert.Equal(Today.AddDays(40 - 12), created.Project.LockDate);
        Assert.Equal(12, created.Project.Terms!.LockDays);
    }

    [Fact]
    public async Task Signing_with_the_stamp_of_an_older_policy_is_refused_and_nothing_is_saved()
    {
        var stampShown = EventPolicy.Default.Stamp();
        SavePolicy(EventPolicy.Keys.LockDays, 14, Today);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Book().HandleAsync(Signing(stamp: stampShown), ManagerId));
        Assert.Empty(_db.WeddingProjects.AsNoTracking());

        await Book().HandleAsync(Signing(stamp: new EventPolicy(LockDays: 14).Stamp()), ManagerId);
        Assert.Equal(14, _db.EventTerms.AsNoTracking().Single().LockDays);
    }

    // ── After a policy change ───────────────────────────────────────────────

    [Fact]
    public async Task A_new_lock_policy_does_not_lock_events_signed_before_it()
    {
        // Signed with 10 days; the Admin then moves the lock to 14 days. The event is 12 days away.
        var signedBefore = SeedEvent(60, daysAway: 12, terms: EventPolicy.Default);
        var signedAfter = SeedEvent(61, daysAway: 12, terms: new EventPolicy(LockDays: 14));
        SavePolicy(EventPolicy.Keys.LockDays, 14, Today);

        Assert.Equal(1, await LockDue());

        var statuses = _db.WeddingProjects.AsNoTracking().ToDictionary(p => p.ProjectId, p => p.Status);
        Assert.Equal(EventStatus.Adjusting, statuses[signedBefore.ProjectId]);
        Assert.Equal(EventStatus.Locked, statuses[signedAfter.ProjectId]);
    }

    [Fact]
    public async Task A_shorter_lock_policy_does_not_leave_signed_events_open_longer()
    {
        // Signed with 10 days, now 7 days: the event 9 days away still locks on its signed date.
        SeedEvent(62, daysAway: 9, terms: EventPolicy.Default);
        SavePolicy(EventPolicy.Keys.LockDays, 7, Today);
        Assert.Equal(1, await LockDue());
    }

    [Fact]
    public async Task Screens_show_the_signed_dates_and_say_the_policy_changed()
    {
        SeedEvent(63, daysAway: 30, terms: EventPolicy.Default);
        SavePolicy(EventPolicy.Keys.LockDays, 14, Today);
        SavePolicy(EventPolicy.Keys.MaxDecreaseRatio, 10, Today);

        var view = (await new ProjectRepository(_db, new ProjectReadModel(_db)).GetProjectAsync("WPSM-2026-063"))!;

        Assert.Equal(Today.AddDays(20), view.LockDate);
        Assert.Equal(70_000_000m, view.MinimumTotal);   // 30% decrease of 100M, as signed (not 10%)
        Assert.True(view.Terms!.DiffersFromCurrent);
        var policies = await new ConfigurationRepository(_db).GetPoliciesAsync();
        Assert.Equal((14, 1), (policies.LockDays, policies.EventsOnOlderTerms));
    }

    [Fact]
    public async Task Settled_events_are_not_counted_as_running_on_older_terms()
    {
        SeedEvent(64, daysAway: -20, terms: EventPolicy.Default, status: EventStatus.Settled);
        SavePolicy(EventPolicy.Keys.LockDays, 14, Today);
        Assert.Equal(0, (await new ConfigurationRepository(_db).GetPoliciesAsync()).EventsOnOlderTerms);
    }

    [Fact]
    public async Task The_supplier_reminder_uses_the_signed_deadline()
    {
        // Signed with 8 days, policy now 12 days: an event 10 days away is not past its deadline yet.
        var project = SeedEvent(65, daysAway: 10, terms: EventPolicy.Default, status: EventStatus.Locked);
        _db.ServiceCatalogItems.Local.Single(c => c.ItemId == 11).RequiresSupplier = true;
        _db.SpecItems.Add(TestFixtures.NewSpecItem(650, project.ProjectId, 11, "Thực đơn", 20, 3_500_000m));
        _db.ProjectManagers.Add(new ProjectManager { ProjectId = project.ProjectId, ManagerId = ManagerId, Role = "Primary", AssignedAt = DateTime.UtcNow });
        SavePolicy(EventPolicy.Keys.LockDays, 14, Today);
        SavePolicy(EventPolicy.Keys.SupplierDeadlineDays, 12, Today);

        var reminders = await new RemindSupplierDeadlines(new EventWorkStore(_db, null!), Messages(), new UnitOfWork(_db)).HandleAsync(Today);
        Assert.Equal(0, reminders);
    }
}

