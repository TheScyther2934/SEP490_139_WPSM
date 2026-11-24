using Application.UseCases.PostEvent;
using Domain.Entities;
using Domain.Rules;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// After the event: ratings (UC-14, GBR-10) and complaints (UC-15, UC-35, UC-36, UC-46). Rules alone first, then the use cases on an
/// in-memory database.
/// </summary>
public sealed class PostEventTests
{
    private const int CustomerId = 1;
    private const int ManagerId = 2;
    private const int AdminId = 3;
    private const int OtherManagerId = 4;

    private readonly WpsmDbContext _db = TestFixtures.NewDb();

    private static readonly RatingInput[] FourRatings =
        [new("Hall", 5, "Sảnh đẹp"), new("menu", 4, "  "), new("Decor", 5, null), new("MC", 3, " MC vui ")];

    // ── Rules without a database ────────────────────────────────────────────

    [Fact]
    public void Ratings_come_after_the_settlement_once_for_all_four_modules()
    {
        Assert.Contains("tất toán", Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateRatings("Completed", false, FourRatings)).Message);
        Assert.Contains("MSG26", Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateRatings("Settled", true, FourRatings)).Message);
        Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateRatings("Settled", false, FourRatings[..3]));
        Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateRatings("Settled", false, [.. FourRatings[..3], new("Bar", 5, null)]));
        Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateRatings("Settled", false, [.. FourRatings[..3], new("MC", 6, null)]));

        var ratings = PostEventPolicy.ValidateRatings("Closed", false, FourRatings);
        Assert.Equal(["Hall", "Menu", "Decor", "MC"], ratings.Select(r => r.Module));
        Assert.Equal([null, null, "MC vui"], ratings.Skip(1).Select(r => r.Comment));
    }

    [Fact]
    public void A_complaint_comes_after_the_event_and_its_code_skips_codes_already_taken()
    {
        Assert.Contains("đã diễn ra", Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateComplaint("Locked", "Âm thanh rè")).Message);
        Assert.Contains("MSG25", Assert.Throws<BusinessRuleException>(() => PostEventPolicy.ValidateComplaint("Completed", " ")).Message);
        Assert.Equal("Âm thanh rè", PostEventPolicy.ValidateComplaint("Completed", " Âm thanh rè "));
        Assert.Equal("CMP-001-01", PostEventPolicy.NextComplaintCode("WPSM-2025-001", 0, []));
        Assert.Equal("CMP-001-03", PostEventPolicy.NextComplaintCode("WPSM-2025-001", 1, ["CMP-001-02"]));
    }

    [Fact]
    public void The_operator_acts_only_on_an_open_complaint_and_the_admin_only_on_an_escalated_one()
    {
        var now = DateTime.UtcNow;
        var open = new Complaint { Status = "Open" };
        Assert.Throws<BusinessRuleException>(() => open.ResolveByOperator(ManagerId, " ", now));
        Assert.Throws<BusinessRuleException>(() => open.Escalate(new string('x', 501), now));
        Assert.Throws<BusinessRuleException>(() => open.ResolveByAdmin(AdminId, "Bồi hoàn", now));
        open.Escalate("Khách đòi bồi hoàn", now);
        Assert.Equal(("Escalated", "Khách đòi bồi hoàn"), (open.Status, open.EscalationNote));
        Assert.Throws<BusinessRuleException>(() => open.ResolveByOperator(ManagerId, "Tự xử lý", now));
        open.ResolveByAdmin(AdminId, "Bồi hoàn 10%", now);
        Assert.Equal(("Resolved", AdminId), (open.Status, open.ResolvedBy));
        Assert.Throws<BusinessRuleException>(() => open.Escalate("Lần nữa", now));
        Assert.Throws<BusinessRuleException>(() => Complaint.ValidateAdminDecision(""));
    }

    // ── Use cases on a database ─────────────────────────────────────────────

    private EventMessages Messages() => new(_db, new TestFixtures.RecordingEmailOutbox(), new EmailOptions());
    private ComplaintRatingRepository Reads() => new(_db);
    private SubmitRatings Rate() => new(new PostEventStore(_db), Reads(), new UnitOfWork(_db));
    private FileComplaint File() => new(new PostEventStore(_db), Reads(), Messages(), new UnitOfWork(_db));
    private RespondToComplaint Respond() => new(new PostEventStore(_db), Reads(), Messages(), new UnitOfWork(_db));
    private ResolveEscalatedComplaint AdminResolve() => new(new PostEventStore(_db), Messages(), new UnitOfWork(_db));

    private async Task SeedAsync(string status)
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(CustomerId, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(AdminId, "Admin", "admin@test.local", "Admin"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.WeddingProjects.Add(TestFixtures.NewProject(10, "WPSM-2025-001", CustomerId, ManagerId, eventDate: TestFixtures.Today.AddDays(-3), status: status));
        _db.Beos.AddRange(
            new Beo { Beoid = 50, ProjectId = 10, Version = 1, Status = "Frozen", Notes = "Bản 1" },
            new Beo { Beoid = 51, ProjectId = 10, Version = 2, Status = "Frozen", Notes = "Bản 2" });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Rating_is_saved_once_after_the_settlement()
    {
        await SeedAsync("Settled");

        var summary = await Rate().HandleAsync(CustomerId, null, FourRatings);

        Assert.True(summary!.IsLocked);
        Assert.False(summary.CanRate);
        Assert.Equal(4, await _db.Ratings.CountAsync());
        await Assert.ThrowsAsync<BusinessRuleException>(() => Rate().HandleAsync(CustomerId, "WPSM-2025-001", FourRatings));
        Assert.Null(await Rate().HandleAsync(CustomerId, "WPSM-2099-999", FourRatings));
    }

    [Fact]
    public async Task Rating_before_the_settlement_is_refused_and_the_summary_says_so()
    {
        await SeedAsync("Completed");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Rate().HandleAsync(CustomerId, null, FourRatings));

        Assert.False((await Reads().GetPostEventSummaryAsync(CustomerId))!.CanRate);
        Assert.Empty(await _db.Ratings.ToListAsync());
    }

    [Fact]
    public async Task Filing_a_complaint_links_the_sheet_version_numbers_it_and_tells_the_operator()
    {
        await SeedAsync("Completed");

        var latest = await File().HandleAsync(CustomerId, null, null, " Âm thanh bị ngắt quãng lúc làm lễ. ");
        var first = await File().HandleAsync(CustomerId, "WPSM-2025-001", 50, "Hoa không đúng màu.");

        Assert.Equal(("CMP-001-01", "Open", 2), (latest.ComplaintCode, latest.Status, latest.BeoVersion));
        Assert.Equal(("CMP-001-02", 1), (first.ComplaintCode, first.BeoVersion));
        Assert.Equal("Âm thanh bị ngắt quãng lúc làm lễ.", latest.Description);
        Assert.Contains(_db.Notifications, n => n.RecipientId == ManagerId && n.LinkUrl == "/manager/projects/WPSM-2025-001/complaints/CMP-001-01");
        await Assert.ThrowsAsync<BusinessRuleException>(() => File().HandleAsync(CustomerId, null, 999, "Sai bản"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => File().HandleAsync(CustomerId, "WPSM-2099-999", null, "Không có"));
    }

    [Fact]
    public async Task A_complaint_before_the_event_is_refused()
    {
        await SeedAsync("Locked");

        await Assert.ThrowsAsync<BusinessRuleException>(() => File().HandleAsync(CustomerId, null, null, "Chưa tổ chức"));

        Assert.Empty(await _db.Complaints.ToListAsync());
    }

    [Fact]
    public async Task The_operator_resolves_a_complaint_and_the_customer_is_told()
    {
        await SeedAsync("Completed");
        var filed = await File().HandleAsync(CustomerId, null, null, "Âm thanh rè");

        var resolved = await Respond().HandleAsync(ManagerId, filed.ComplaintCode, "resolve", " Đã giảm 5% chi phí âm thanh ");

        Assert.Equal(("Resolved", "Đã giảm 5% chi phí âm thanh", "Manager"), (resolved!.Status, resolved.ResolutionNote, resolved.ResolvedByName));
        Assert.Contains(_db.Notifications, n => n.RecipientId == CustomerId && n.LinkUrl == "/customer/complaints/CMP-001-01");
        Assert.Null(await Respond().HandleAsync(OtherManagerId, filed.ComplaintCode, "Resolve", "Không phải sự kiện của tôi"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Respond().HandleAsync(ManagerId, filed.ComplaintCode, "Escalate", "Muộn"));
    }

    [Fact]
    public async Task An_escalated_complaint_waits_for_the_admin_who_resolves_it()
    {
        await SeedAsync("Completed");
        var filed = await File().HandleAsync(CustomerId, null, null, "Thiếu bàn");
        await Assert.ThrowsAsync<BusinessRuleException>(() => Respond().HandleAsync(ManagerId, filed.ComplaintCode, "Close", "x"));

        var escalated = await Respond().HandleAsync(ManagerId, filed.ComplaintCode, "Escalate", "Khách đòi bồi hoàn");
        await Assert.ThrowsAsync<BusinessRuleException>(() => Respond().HandleAsync(ManagerId, filed.ComplaintCode, "Resolve", "Tự xử lý"));
        Assert.Contains(_db.Notifications, n => n.RecipientId == AdminId && n.LinkUrl == "/admin/complaints");

        await Assert.ThrowsAsync<BusinessRuleException>(() => AdminResolve().HandleAsync(filed.ComplaintId, " ", AdminId));
        Assert.False(await AdminResolve().HandleAsync(999, "Bồi hoàn", AdminId));
        Assert.True(await AdminResolve().HandleAsync(filed.ComplaintId, "Bồi hoàn 10% giá trị bàn", AdminId));

        var complaint = await _db.Complaints.AsNoTracking().SingleAsync();
        Assert.Equal(("Escalated", "Resolved", AdminId), (escalated!.Status, complaint.Status, complaint.ResolvedBy));
        Assert.Equal(2, await _db.Notifications.CountAsync(n => n.Message.Contains("đã được Admin giải quyết")));
        await Assert.ThrowsAsync<BusinessRuleException>(() => AdminResolve().HandleAsync(filed.ComplaintId, "Lần hai", AdminId));
    }
}
