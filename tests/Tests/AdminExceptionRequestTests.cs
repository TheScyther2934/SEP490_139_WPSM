using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// A-18 Special Exception Requests (UC-47): unresolvable resource conflicts escalated by an operator,
/// decided by an Admin with a mandatory note and an audit trail.
/// </summary>
public sealed class AdminExceptionRequestTests
{
    private const string ProjectCode = "WPSM-2026-014";
    private const int CustomerId = 1;
    private const int ManagerId = 2;
    private const int AdminId = 3;
    private const int ResourceRequestId = 2;
    private const int DecidedRequestId = 3;

    private readonly WpsmDbContext _db;
    private readonly AdminRepository _repo;

    public AdminExceptionRequestTests()
    {
        _db = TestFixtures.NewDb();
        _repo = new AdminRepository(_db);
    }

    // ── Seed + helpers ────────────────────────────────────────────────────────

    private async Task SeedAsync()
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(CustomerId, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(AdminId, "Admin", "admin@test.local", "Admin Tổng"),
            TestFixtures.NewUser(4, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.Add(TestFixtures.NewCatalogItem(11, "Thực đơn tiệc 8 món", "Catering", "Bàn", 3_500_000m));
        _db.PriceFrames.Add(TestFixtures.NewFrame(11, 3_000_000m, 4_000_000m));
        _db.WeddingProjects.AddRange(
            TestFixtures.NewProject(14, ProjectCode, CustomerId, ManagerId, eventDate: TestFixtures.Today.AddDays(10), status: "Locked", frozen: true),
            TestFixtures.NewProject(15, "WPSM-2026-015", CustomerId, ManagerId, eventDate: TestFixtures.Today.AddDays(10), status: "Locked", frozen: true));
        _db.SpecItems.Add(TestFixtures.NewSpecItem(101, 14, 11, "Thực đơn tiệc 8 món", 30, 3_500_000m));
        _db.Resources.Add(new Resource { ResourceId = 1, Name = "Dàn âm thanh JBL", Type = "Audio", TotalQuantity = 10 });
        _db.ResourceAllocations.AddRange(
            new ResourceAllocation { AllocationId = 201, ResourceId = 1, HallId = 1, ProjectId = 15, StartTime = At(10), EndTime = At(13), AllocatedQuantity = 8, Status = "Reserved" },
            new ResourceAllocation { AllocationId = 202, ResourceId = 1, HallId = 1, ProjectId = 14, StartTime = At(10), EndTime = At(13), AllocatedQuantity = 5, Status = "PendingException" },
            new ResourceAllocation { AllocationId = 203, ResourceId = 1, HallId = 1, ProjectId = 14, StartTime = At(15), EndTime = At(17), AllocatedQuantity = 3, Status = "PendingException" },
            new ResourceAllocation { AllocationId = 204, ResourceId = 1, HallId = 1, ProjectId = 15, StartTime = At(18), EndTime = At(20), AllocatedQuantity = 2, Status = "Released" });
        _db.ExceptionRequests.AddRange(
            new ExceptionRequest
            {
                ExceptionRequestId = 1, RequestCode = $"EXC-{TestFixtures.Today.Year}-001", SourceType = "ResourceConflict",
                ProjectId = 14, AllocationId = 203, RequestedBy = ManagerId, RequestNote = "Cần thêm loa cho tiết mục văn nghệ",
                Status = "Pending", CreatedAt = DateTime.UtcNow.AddHours(-5)
            },
            new ExceptionRequest
            {
                ExceptionRequestId = ResourceRequestId, RequestCode = $"EXC-{TestFixtures.Today.Year}-002", SourceType = "ResourceConflict",
                ProjectId = 14, AllocationId = 202, RequestedBy = ManagerId, RequestNote = "Không còn dàn âm thanh nào khác",
                Status = "Pending", CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new ExceptionRequest
            {
                ExceptionRequestId = DecidedRequestId, RequestCode = $"EXC-{TestFixtures.Today.Year}-003", SourceType = "ResourceConflict",
                ProjectId = 15, AllocationId = 204, RequestedBy = ManagerId, RequestNote = "Yêu cầu cũ",
                Status = "Approved", DecisionBy = AdminId, DecisionNote = "Đã chấp thuận trước đó",
                DecidedAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow.AddHours(-6)
            });
        await _db.SaveChangesAsync();
    }

    private DateTime At(int hour) =>
        DateTime.SpecifyKind(TestFixtures.Today.AddDays(10).ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    // ── A-18 queue ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQueue_OrdersPendingRequestsBeforeDecidedOnes()
    {
        await SeedAsync();

        var queue = await _repo.GetExceptionRequestsAsync();

        Assert.Equal(3, queue.Items.Count);
        Assert.Equal(1, queue.Items[0].Id);
        Assert.Equal(ResourceRequestId, queue.Items[1].Id);
        Assert.Equal(DecidedRequestId, queue.Items[2].Id);
    }

    [Fact]
    public async Task GetQueue_SummarisesStatusesAndOldestPendingAge()
    {
        await SeedAsync();

        var queue = await _repo.GetExceptionRequestsAsync();

        Assert.Equal(2, queue.Summary.Pending);
        Assert.Equal(1, queue.Summary.Approved);
        Assert.Equal(0, queue.Summary.Rejected);
        Assert.NotNull(queue.Summary.OldestPendingHours);
        Assert.InRange(queue.Summary.OldestPendingHours!.Value, 4, 6);
    }

    [Fact]
    public async Task GetQueue_ComputesTheResourceShortageAndClashingEvents()
    {
        await SeedAsync();

        var queue = await _repo.GetExceptionRequestsAsync();

        var resource = queue.Items.Single(i => i.Id == ResourceRequestId).Resource!;
        Assert.Equal(202, resource.AllocationId);
        Assert.Equal(5, resource.Quantity);
        Assert.Equal(10, resource.TotalQuantity);
        Assert.Equal(8, resource.OtherQuantity);
        Assert.Equal(3, resource.Shortage);
        var clash = Assert.Single(resource.Clashing);
        Assert.Equal("WPSM-2026-015", clash.ProjectCode);
        Assert.Equal(8, clash.Quantity);
        Assert.Equal("Quản lý Minh", queue.Items.Single(i => i.Id == ResourceRequestId).RequestedByName);
    }

    [Fact]
    public async Task GetQueue_ReportsTheDecisionAuditTrail()
    {
        await SeedAsync();

        var queue = await _repo.GetExceptionRequestsAsync();

        var decided = queue.Items.Single(i => i.Id == DecidedRequestId);
        Assert.Equal("Approved", decided.Status);
        Assert.Equal("Đã chấp thuận trước đó", decided.DecisionNote);
        Assert.Equal("Admin Tổng", decided.DecidedByName);
        Assert.NotNull(decided.DecidedAt);
    }

    // ── UC-47 decision ──────────────────────────────────────────────────────

    [Fact]
    public async Task Decide_RequiresADecisionNote()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.DecideExceptionAsync(1, approve: true, note: "   ", AdminId));

        Assert.Contains("Ghi chú quyết định", ex.Message);
    }

    [Fact]
    public async Task Decide_RejectsADecisionNoteLongerThan300Characters()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.DecideExceptionAsync(1, approve: true, note: new string('n', 301), AdminId));

        Assert.Contains("300", ex.Message);
    }

    [Fact]
    public async Task Decide_ReturnsFalseForAnUnknownRequest()
    {
        await SeedAsync();

        var decided = await _repo.DecideExceptionAsync(999, approve: true, note: "Hợp lệ", AdminId);

        Assert.False(decided);
    }

    [Fact]
    public async Task Decide_RejectsARequestThatWasAlreadyDecided()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.DecideExceptionAsync(DecidedRequestId, approve: true, note: "Duyệt lại", AdminId));

        Assert.Contains("đã được quyết định", ex.Message);
    }

    [Fact]
    public async Task Decide_ApproveResourceExceptionMarksTheAllocationApproved()
    {
        await SeedAsync();

        var decided = await _repo.DecideExceptionAsync(ResourceRequestId, approve: true, note: "Cho phép vượt công suất", AdminId);

        Assert.True(decided);
        var allocation = await _db.ResourceAllocations.SingleAsync(a => a.AllocationId == 202);
        Assert.Equal("Exception Approved", allocation.Status);
        Assert.True(allocation.IsExceptionApproved);
        Assert.Equal(AdminId, allocation.ExceptionApprovedBy);
        Assert.Equal("Cho phép vượt công suất", allocation.ExceptionNote);
        Assert.NotNull(allocation.ExceptionApprovedAt);
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == ManagerId && n.Message.Contains("chấp thuận")));
    }

    [Fact]
    public async Task Decide_RejectResourceExceptionReleasesThePendingAllocation()
    {
        await SeedAsync();

        var decided = await _repo.DecideExceptionAsync(ResourceRequestId, approve: false, note: "Không thể bố trí thêm thiết bị", AdminId);

        Assert.True(decided);
        var allocation = await _db.ResourceAllocations.SingleAsync(a => a.AllocationId == 202);
        Assert.Equal("Released", allocation.Status);
        Assert.False(allocation.IsExceptionApproved);
        Assert.Equal("Rejected", (await _db.ExceptionRequests.SingleAsync(r => r.ExceptionRequestId == ResourceRequestId)).Status);
    }
}
