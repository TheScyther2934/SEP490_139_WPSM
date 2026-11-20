using Application.Persistence;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// The event stage flow: booking window (15–90 days), stage 2 adjustments with a note and the 70% floor, the automatic lock
/// 10 days before, supplier evidence reminders, addon contracts confirmed by the customer, and cancellation without refund.
/// </summary>
public sealed class EventStageFlowTests : IDisposable
{
    private const int CustomerId = 1, ManagerId = 2, OtherManagerId = 3;
    private const string Code = "WPSM-2026-050";
    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly TestFixtures.RecordingEmailOutbox _outbox = new();
    private static DateOnly Today => VietnamTime.GetVietnamToday();

    public EventStageFlowTests()
    {
        _db.Users.AddRange(TestFixtures.NewUser(CustomerId, "Customer", "khach@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "other@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.AddRange(
            TestFixtures.NewCatalogItem(10, "Thuê sảnh", "Hall", "Gói", 30_000_000m),
            TestFixtures.NewCatalogItem(11, "Thực đơn", "Catering", "Bàn", 3_500_000m),
            TestFixtures.NewCatalogItem(12, "Hoa tươi", "Decor", "Gói", 10_000_000m));
        _db.ServiceCatalogItems.Local.Single(c => c.ItemId == 11).RequiresSupplier = true;
        _db.ServiceCatalogItems.Local.Single(c => c.ItemId == 12).RequiresSupplier = true;
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // 30M hall + 20 tables x 3.5M + 10M flowers = 110M signed.
    private WeddingProject SeedEvent(int daysAway, string status = "Adjusting")
    {
        var project = TestFixtures.NewProject(50, Code, CustomerId, ManagerId, eventDate: Today.AddDays(daysAway), status: status, baseValue: 110_000_000m);
        _db.WeddingProjects.Add(project);
        _db.SpecItems.AddRange(TestFixtures.NewSpecItem(500, 50, 10, "Thuê sảnh", 1, 30_000_000m),
            TestFixtures.NewSpecItem(501, 50, 11, "Thực đơn", 20, 3_500_000m),
            TestFixtures.NewSpecItem(502, 50, 12, "Hoa tươi", 1, 10_000_000m));
        _db.ProjectManagers.Add(new ProjectManager { ProjectId = 50, ManagerId = ManagerId, Role = "Primary", AssignedAt = DateTime.UtcNow });
        _db.Payments.Add(new Payment { ProjectId = 50, PaymentType = "Deposit", Status = "Paid", Amount = 33_000_000m, PaymentMethod = "Cash", PaidDate = Today, RecordedBy = ManagerId });
        _db.SaveChanges();
        return project;
    }

    private static SpecificationCommand Lines(int tables, string? note = "Khách giảm bàn vì danh sách khách mời thay đổi") =>
        new("", 0, [new(10, "Thuê sảnh", 1, 30_000_000m), new(11, "Thực đơn", tables, 3_500_000m), new(12, "Hoa tươi", 1, 10_000_000m)], note);

    private EventFlowRepository Events() => new(_db, _outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });
    private AddonContractRepository Addons() => new(_db, _outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });

    // ── Rules ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void Booking_is_accepted_only_15_to_90_days_ahead(int days, bool ok)
    {
        var error = EventStageRules.ValidateBookingDate(Today.AddDays(days), Today, EventPolicy.Default);
        Assert.Equal(ok, error is null);
    }

    [Fact]
    public void Floor_is_70_percent_of_the_signed_value_and_increases_are_free()
    {
        Assert.Equal(70_000_000m, EventStageRules.MinimumTotal(100_000_000m, EventPolicy.Default));
        Assert.Null(EventStageRules.ValidateAdjustedTotal(100_000_000m, 70_000_000m, EventPolicy.Default));
        Assert.NotNull(EventStageRules.ValidateAdjustedTotal(100_000_000m, 69_999_999m, EventPolicy.Default));
        Assert.Null(EventStageRules.ValidateAdjustedTotal(100_000_000m, 300_000_000m, EventPolicy.Default));
        Assert.Equal(30_000_000m, EventStageRules.DepositFor(100_000_000m, EventPolicy.Default));
    }

    [Fact]
    public void Lock_and_supplier_dates_follow_the_event_date()
    {
        var eventDate = new DateOnly(2027, 1, 20);
        Assert.Equal(new DateOnly(2027, 1, 10), EventStageRules.LockDate(eventDate, EventPolicy.Default));
        Assert.Equal(new DateOnly(2027, 1, 12), EventStageRules.SupplierDeadline(eventDate, EventPolicy.Default));
        Assert.Equal(new DateOnly(2027, 1, 27), EventStageRules.SettlementDueDate(eventDate, EventPolicy.Default));
        Assert.True(EventStageRules.ShouldLock("Adjusting", eventDate, new DateOnly(2027, 1, 10), EventPolicy.Default));
        Assert.False(EventStageRules.ShouldLock("Adjusting", eventDate, new DateOnly(2027, 1, 9), EventPolicy.Default));
        var timeline = EventStageRules.Timeline("Locked", eventDate, new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 15), EventPolicy.Default);
        Assert.Equal(["Done", "Done", "Current", "Upcoming", "Upcoming"], timeline.Select(s => s.State));
    }

    // ── Stage 2: adjustments ────────────────────────────────────────────────

    [Fact]
    public async Task Adjustment_within_the_floor_is_saved_with_a_note_and_before_after_totals()
    {
        SeedEvent(40);
        var view = await TestFixtures.NewSpecificationRepository(_db).UpdateSpecificationAsync(Code, ManagerId, Lines(15));

        Assert.NotNull(view);
        Assert.Equal(92_500_000m, view!.CurrentTotal);
        var note = await _db.StageNotes.SingleAsync();
        Assert.Equal("Adjustment", note.NoteType);
        Assert.Equal(110_000_000m, note.ValueBefore);
        Assert.Equal(92_500_000m, note.ValueAfter);
        Assert.Equal(2, note.Stage);
    }

    [Fact]
    public async Task Adjustment_below_the_floor_is_refused()
    {
        SeedEvent(40);
        // 30 + 4 x 3.5 + 10 = 54M < 77M (70% of 110M)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestFixtures.NewSpecificationRepository(_db).UpdateSpecificationAsync(Code, ManagerId, Lines(4)));
        Assert.Contains("77.000.000", ex.Message.Replace(",", "."));
        Assert.Equal(110_000_000m, (await _db.WeddingProjects.SingleAsync()).CurrentTotal);
    }

    [Fact]
    public async Task Adjustment_needs_a_note()
    {
        SeedEvent(40);
        await Assert.ThrowsAsync<InvalidOperationException>(() => TestFixtures.NewSpecificationRepository(_db).UpdateSpecificationAsync(Code, ManagerId, Lines(18, note: " ")));
    }

    [Fact]
    public async Task Adjustment_after_the_lock_date_is_refused_even_before_the_job_runs()
    {
        SeedEvent(9);   // lock date passed, status still Adjusting
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestFixtures.NewSpecificationRepository(_db).UpdateSpecificationAsync(Code, ManagerId, Lines(25)));
        Assert.Contains("hợp đồng phát sinh", ex.Message);
    }

    // ── Stage 3: lock, suppliers ────────────────────────────────────────────

    [Fact]
    public async Task Lock_job_locks_on_the_lock_date_and_tells_the_customer_who_to_contact()
    {
        SeedEvent(10);
        var locked = await Events().LockDueEventsAsync(Today);

        Assert.Equal(1, locked);
        var p = await _db.WeddingProjects.SingleAsync();
        Assert.Equal("Locked", p.Status);
        Assert.NotNull(p.LockedAt);
        Assert.Contains(_db.Notifications, n => n.RecipientId == CustomerId && n.Type == "Stage reminder");
        Assert.Contains(_db.Notifications, n => n.RecipientId == ManagerId && n.LinkUrl!.EndsWith("/suppliers"));
        Assert.Contains(_outbox.Messages, m => m.To == "khach@test.local");
    }

    [Fact]
    public async Task Lock_job_leaves_events_still_in_the_adjusting_window()
    {
        SeedEvent(11);
        Assert.Equal(0, await Events().LockDueEventsAsync(Today));
        Assert.Equal("Adjusting", (await _db.WeddingProjects.SingleAsync()).Status);
    }

    [Fact]
    public async Task Supplier_reminder_lists_missing_evidence_once_a_day()
    {
        SeedEvent(7, "Locked");   // supplier deadline (8 days before) passed
        _db.StoredFiles.Add(new StoredFile { FileId = Guid.NewGuid(), Purpose = "SupplierEvidence", StorageKey = "k", OriginalName = "a.pdf", ContentType = "application/pdf", SizeBytes = 1, UploadedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        _db.SupplierEvidences.Add(new SupplierEvidence { ProjectId = 50, SpecItemId = 501, SupplierName = "Bếp ABC", EvidenceType = "Contract", FileId = _db.StoredFiles.Local.First().FileId, UploadedBy = ManagerId, UploadedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await Events().RemindSupplierDeadlinesAsync(Today));
        Assert.Contains(_db.Notifications, n => n.RecipientId == ManagerId && n.Message.Contains("Hoa tươi") && !n.Message.Contains("Thực đơn"));
        Assert.Equal(0, await Events().RemindSupplierDeadlinesAsync(Today));
    }

    // ── Addon contracts ─────────────────────────────────────────────────────

    [Fact]
    public async Task Addon_contract_is_only_for_locked_events()
    {
        SeedEvent(40);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Addons().CreateAsync(Code, ManagerId, new("Thêm bàn", null, [new(11, "Bàn thêm", 2, 3_500_000m)])));
        Assert.Contains("điều chỉnh", ex.Message);
    }

    [Fact]
    public async Task Addon_contract_counts_only_after_the_customer_confirms()
    {
        SeedEvent(5, "Locked");
        var created = await Addons().CreateAsync(Code, ManagerId, new("Thêm 2 bàn", "Khách báo qua điện thoại", [new(11, "Bàn thêm", 2, 3_500_000m)]));

        Assert.Equal("PendingCustomer", created.Status);
        Assert.Equal(7_000_000m, created.Total);
        Assert.Equal(0m, await EventFlowSupport.ConfirmedAddonTotalAsync(_db, 50, default));
        Assert.Contains(_db.Notifications, n => n.RecipientId == CustomerId && n.Type == "Addon contract");

        var confirmed = await Addons().ConfirmByCustomerAsync(CustomerId, created.Code);
        Assert.Equal("Confirmed", confirmed!.Status);
        Assert.Equal(7_000_000m, await EventFlowSupport.ConfirmedAddonTotalAsync(_db, 50, default));
        Assert.Contains(_db.Notifications, n => n.RecipientId == ManagerId && n.Message.Contains(created.Code));
        // Another customer cannot confirm it.
        Assert.Null(await Addons().ConfirmByCustomerAsync(99, created.Code));
    }

    [Fact]
    public async Task Addon_confirmed_after_the_invoice_is_added_to_the_settlement()
    {
        var p = SeedEvent(-1, "Completed");
        var invoice = await EventFlowSupport.IssueInvoiceAsync(_db, p, ManagerId, EventPolicy.Default, default);
        await _db.SaveChangesAsync();
        Assert.Equal(77_000_000m, invoice.ContractTotal - invoice.DepositPaid);

        var addon = await Addons().CreateAsync(Code, ManagerId, new("Kéo dài giờ tiệc", null, [new(null, "Phụ phí thêm giờ", 1, 5_000_000m)]));
        await Addons().ConfirmByCustomerAsync(CustomerId, addon.Code);

        var reloaded = await _db.SettlementInvoices.Include(i => i.Lines).SingleAsync();
        Assert.Equal(5_000_000m, reloaded.AddonTotal);
        Assert.Contains(reloaded.Lines, l => l.LineType == "Addon" && l.Amount == 5_000_000m);
        Assert.Equal(82_000_000m, (await _db.Payments.SingleAsync(x => x.PaymentType == "Settlement")).Amount);
    }

    [Fact]
    public async Task Cancelled_event_frees_the_slot_and_keeps_the_deposit()
    {
        SeedEvent(40);
        var view = await Events().CancelEventAsync(Code, ManagerId, "Gia đình dời đám cưới sang năm sau");

        Assert.Equal("Cancelled", view!.Status);
        var p = await _db.WeddingProjects.SingleAsync();
        Assert.Equal("Gia đình dời đám cưới sang năm sau", p.CancelReason);
        Assert.Equal("Paid", (await _db.Payments.SingleAsync(x => x.PaymentType == "Deposit")).Status);   // never refunded
        Assert.Contains(_db.StageNotes, n => n.Content.Contains("không hoàn cọc"));
    }

    // ── Customer view ───────────────────────────────────────────────────────

    [Fact]
    public async Task Customer_sees_services_stages_contacts_and_hotline_read_only()
    {
        SeedEvent(5, "Locked");
        _db.SystemSettings.Add(new SystemSetting { SettingKey = "Hotline", SettingValue = "0976586668", UpdatedAt = DateTime.UtcNow });
        _db.ProjectManagers.Add(new ProjectManager { ProjectId = 50, ManagerId = OtherManagerId, Role = "CoManager", AssignedAt = DateTime.UtcNow.AddMinutes(1) });
        await _db.SaveChangesAsync();

        var view = await Events().GetCustomerEventAsync(CustomerId, null);

        Assert.NotNull(view);
        Assert.False(view!.CanAdjust);
        Assert.Equal(3, view.CurrentStage);
        Assert.Equal(3, view.Services.Count);
        Assert.Equal(["Quản lý Minh", "Quản lý Khác"], view.Contacts.Select(c => c.FullName));
        Assert.Equal("0976586668", view.Hotline);
        Assert.Null(await Events().GetCustomerEventAsync(99, Code));
    }
}
