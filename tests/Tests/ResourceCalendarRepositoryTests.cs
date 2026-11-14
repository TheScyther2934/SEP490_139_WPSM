using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// FE-07 Resource Calendar — M-21/M-22 allocation, conflict detection (GBR-09) and the UC-47
/// escalation for an unresolvable over-allocation.
/// </summary>
public sealed class ResourceCalendarRepositoryTests
{
    private const string ProjectCode = "WPSM-2026-014";
    private const int ManagerId = 2;
    private const int OtherManagerId = 4;
    private const int AudioResourceId = 1;
    private const int LightResourceId = 2;

    private readonly WpsmDbContext _db;
    private readonly ResourceCalendarRepository _repo;
    private readonly DateOnly _eventDate = TestFixtures.Today.AddDays(10);

    public ResourceCalendarRepositoryTests()
    {
        _db = TestFixtures.NewDb();
        _repo = new ResourceCalendarRepository(_db);
    }

    // ── Seed + helpers ────────────────────────────────────────────────────────

    private async Task SeedAsync(string secondProjectStatus = "Baseline Frozen")
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(1, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(3, "Admin", "admin@test.local", "Admin"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.Resources.AddRange(
            new Resource { ResourceId = AudioResourceId, Name = "Dàn âm thanh JBL", Type = "Audio", TotalQuantity = 10 },
            new Resource { ResourceId = LightResourceId, Name = "Đèn follow", Type = "Follow Light", TotalQuantity = 4 });
        _db.WeddingProjects.AddRange(
            TestFixtures.NewProject(14, ProjectCode, 1, ManagerId, eventDate: _eventDate, status: "Baseline Frozen", frozen: true),
            TestFixtures.NewProject(15, "WPSM-2026-015", 1, ManagerId, eventDate: _eventDate, status: secondProjectStatus, frozen: true),
            TestFixtures.NewProject(16, "WPSM-2026-016", 1, OtherManagerId, eventDate: _eventDate, status: "Baseline Frozen", frozen: true));
        await _db.SaveChangesAsync();
    }

    private async Task<ResourceAllocation> AddAllocationAsync(
        int projectId,
        int quantity,
        int resourceId = AudioResourceId,
        int startHour = 10,
        int endHour = 13,
        string status = "Reserved")
    {
        var allocation = new ResourceAllocation
        {
            ResourceId = resourceId,
            HallId = 1,
            ProjectId = projectId,
            StartTime = At(startHour),
            EndTime = At(endHour),
            AllocatedQuantity = quantity,
            Status = status
        };
        _db.ResourceAllocations.Add(allocation);
        await _db.SaveChangesAsync();
        return allocation;
    }

    private DateTime At(int hour) =>
        DateTime.SpecifyKind(_eventDate.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    private ResourceAllocationCommand Command(
        int projectId = 14,
        int resourceId = AudioResourceId,
        int quantity = 4,
        int startHour = 10,
        int endHour = 13,
        bool requestException = false,
        string? note = null) =>
        new(resourceId, projectId, At(startHour), At(endHour), quantity, requestException, note);

    // ── M-21 save ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_CreatesAReservedAllocation()
    {
        await SeedAsync();

        var result = await _repo.SaveAsync(null, Command(), ManagerId);

        Assert.NotNull(result);
        Assert.Null(result!.Conflict);
        Assert.NotNull(result.Allocation);
        Assert.Equal("Reserved", result.Allocation!.Status);
        Assert.Equal(4, result.Allocation.Quantity);
        Assert.Equal("Dàn âm thanh JBL", result.Allocation.ResourceName);
        Assert.False(result.Allocation.HasConflict);
        Assert.Equal(1, await _db.ResourceAllocations.CountAsync());
    }

    [Fact]
    public async Task Save_ReturnsTheConflictAndPersistsNothingWhenOverCapacity()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);

        var result = await _repo.SaveAsync(null, Command(quantity: 5), ManagerId);

        Assert.NotNull(result);
        Assert.Null(result!.Allocation);
        Assert.NotNull(result.Conflict);
        Assert.Equal(8, result.Conflict!.OtherQuantity);
        Assert.Equal(3, result.Conflict.Shortage);
        Assert.Single(result.Conflict.Clashing);
        Assert.Equal(1, await _db.ResourceAllocations.CountAsync());
    }

    [Fact]
    public async Task Save_WithExceptionRequestCreatesAPendingAllocationAndTicket()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);

        var result = await _repo.SaveAsync(null, Command(quantity: 5, requestException: true,
            note: "Không còn dàn âm thanh nào khác trong ngày"), ManagerId);

        Assert.NotNull(result);
        Assert.Null(result!.Conflict);
        Assert.Equal("PendingException", result.Allocation!.Status);
        var request = await _db.ExceptionRequests.SingleAsync();
        Assert.Equal("ResourceConflict", request.SourceType);
        Assert.Equal("Pending", request.Status);
        Assert.Equal($"EXC-{TestFixtures.Today.Year}-001", request.RequestCode);
        Assert.Equal("Không còn dàn âm thanh nào khác trong ngày", request.RequestNote);
    }

    [Fact]
    public async Task Save_ExceptionRequestRequiresANote()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, Command(quantity: 5, requestException: true, note: "   "), ManagerId));

        Assert.Contains("Ghi chú gửi Admin", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsQuantityAboveTheResourceTotal()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, Command(quantity: 11), ManagerId));

        Assert.Contains("vượt quá tổng", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsZeroQuantity()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, Command(quantity: 0), ManagerId));

        Assert.Contains("lớn hơn 0", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsAnEndTimeBeforeTheStartTime()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, Command(startHour: 14, endHour: 12), ManagerId));

        Assert.Contains("Giờ kết thúc", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsAWindowOutsideTheEventDate()
    {
        await SeedAsync();
        var command = new ResourceAllocationCommand(AudioResourceId, 14,
            At(10), At(13).AddDays(1), 4);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, command, ManagerId));

        Assert.Contains("ngày diễn ra tiệc", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsASettledProject()
    {
        await SeedAsync(secondProjectStatus: "Settled");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(null, Command(projectId: 15), ManagerId));

        Assert.Contains("đã quyết toán", ex.Message);
    }

    [Fact]
    public async Task Save_ReturnsNullForAnotherManagersProject()
    {
        await SeedAsync();

        var result = await _repo.SaveAsync(null, Command(), OtherManagerId);

        Assert.Null(result);
    }

    [Fact]
    public async Task Save_ReschedulesAnExistingReservedAllocation()
    {
        await SeedAsync();
        var existing = await AddAllocationAsync(14, quantity: 4);

        var result = await _repo.SaveAsync(existing.AllocationId, Command(quantity: 6, startHour: 11, endHour: 14), ManagerId);

        Assert.NotNull(result);
        Assert.NotNull(result!.Allocation);
        Assert.Equal(existing.AllocationId, result.Allocation!.AllocationId);
        Assert.Equal(6, result.Allocation.Quantity);
        Assert.Equal(11, result.Allocation.StartTime.Hour);
        Assert.Equal(1, await _db.ResourceAllocations.CountAsync());
    }

    [Fact]
    public async Task Save_RejectsEditsWhileTheExceptionIsPending()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);
        var pending = (await _repo.SaveAsync(null, Command(quantity: 5, requestException: true, note: "Xin ngoại lệ"), ManagerId))!;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(pending.Allocation!.AllocationId, Command(quantity: 4), ManagerId));

        Assert.Contains("đang chờ Admin", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsEditsAfterTheExceptionWasApproved()
    {
        await SeedAsync();
        var approved = await AddAllocationAsync(14, quantity: 5, status: "Exception Approved");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(approved.AllocationId, Command(quantity: 4), ManagerId));

        Assert.Contains("đã được Admin chấp thuận", ex.Message);
    }

    [Fact]
    public async Task Save_RejectsEditsAfterTheAllocationWasReleased()
    {
        await SeedAsync();
        var released = await AddAllocationAsync(14, quantity: 4, status: "Released");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveAsync(released.AllocationId, Command(quantity: 4), ManagerId));

        Assert.Contains("đã được thu hồi", ex.Message);
    }

    [Fact]
    public async Task Save_DoesNotTreatAdjacentTimeRangesAsAConflict()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 6, startHour: 8, endHour: 10);

        var result = await _repo.SaveAsync(null, Command(quantity: 6, startHour: 10, endHour: 13), ManagerId);

        Assert.NotNull(result);
        Assert.Null(result!.Conflict);
        Assert.NotNull(result.Allocation);
    }

    [Fact]
    public async Task Save_GeneratesSequentialExceptionCodes()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);
        await AddAllocationAsync(15, quantity: 3, resourceId: LightResourceId);

        await _repo.SaveAsync(null, Command(quantity: 5, requestException: true, note: "Ngoại lệ một"), ManagerId);
        await _repo.SaveAsync(null, Command(quantity: 3, resourceId: LightResourceId, requestException: true, note: "Ngoại lệ hai"), ManagerId);

        var codes = await _db.ExceptionRequests.OrderBy(e => e.ExceptionRequestId).Select(e => e.RequestCode).ToListAsync();
        Assert.Equal([$"EXC-{TestFixtures.Today.Year}-001", $"EXC-{TestFixtures.Today.Year}-002"], codes);
    }

    // ── Release ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Release_SetsTheAllocationToReleased()
    {
        await SeedAsync();
        var allocation = await AddAllocationAsync(14, quantity: 4);

        var released = await _repo.ReleaseAsync(allocation.AllocationId, ManagerId);

        Assert.NotNull(released);
        Assert.Equal("Released", released!.Status);
        Assert.Equal("Released", (await _db.ResourceAllocations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Release_RejectsASecondRelease()
    {
        await SeedAsync();
        var allocation = await AddAllocationAsync(14, quantity: 4, status: "Released");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.ReleaseAsync(allocation.AllocationId, ManagerId));

        Assert.Contains("đã được thu hồi", ex.Message);
    }

    [Fact]
    public async Task Release_RejectsAPendingExceptionAllocation()
    {
        await SeedAsync();
        var pending = await AddAllocationAsync(14, quantity: 4, status: "PendingException");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.ReleaseAsync(pending.AllocationId, ManagerId));

        Assert.Contains("chờ Admin", ex.Message);
    }

    [Fact]
    public async Task Release_ReturnsNullForAnotherManagersAllocation()
    {
        await SeedAsync();
        var allocation = await AddAllocationAsync(16, quantity: 4);

        var released = await _repo.ReleaseAsync(allocation.AllocationId, ManagerId);

        Assert.Null(released);
    }

    // ── UC-47 escalation ────────────────────────────────────────────────────

    [Fact]
    public async Task RequestException_CreatesAPendingTicket()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);
        var mine = await AddAllocationAsync(14, quantity: 5);

        var detail = await _repo.RequestExceptionAsync(mine.AllocationId, "Xin phép vượt công suất", ManagerId);

        Assert.NotNull(detail);
        Assert.Equal("PendingException", detail!.Status);
        Assert.NotNull(detail.PendingRequestCode);
        Assert.Equal(3, detail.Shortage);
        var request = await _db.ExceptionRequests.SingleAsync();
        Assert.Equal(mine.AllocationId, request.AllocationId);
        Assert.Equal("Xin phép vượt công suất", request.RequestNote);
    }

    [Fact]
    public async Task RequestException_RequiresANote()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);
        var mine = await AddAllocationAsync(14, quantity: 5);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(mine.AllocationId, " ", ManagerId));

        Assert.Contains("Ghi chú gửi Admin", ex.Message);
    }

    [Fact]
    public async Task RequestException_IsRejectedWhenThereIsNoConflict()
    {
        await SeedAsync();
        var mine = await AddAllocationAsync(14, quantity: 4);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(mine.AllocationId, "Xin phép", ManagerId));

        Assert.Contains("không còn xung đột", ex.Message);
    }

    [Fact]
    public async Task RequestException_RejectsASecondRequestForTheSameAllocation()
    {
        await SeedAsync();
        await AddAllocationAsync(15, quantity: 8);
        var mine = await AddAllocationAsync(14, quantity: 5);
        await _repo.RequestExceptionAsync(mine.AllocationId, "Lần một", ManagerId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(mine.AllocationId, "Lần hai", ManagerId));

        Assert.Contains("Chỉ phân bổ đang hiệu lực", ex.Message);
        Assert.Single(await _db.ExceptionRequests.ToListAsync());
    }

    // ── M-21 calendar grid ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCalendar_SplitsReservedApprovedAndPendingQuantities()
    {
        await SeedAsync();
        await AddAllocationAsync(14, quantity: 6, status: "Reserved");
        await AddAllocationAsync(15, quantity: 4, status: "Exception Approved");
        var pending = await AddAllocationAsync(15, quantity: 3, status: "PendingException");
        _db.ExceptionRequests.Add(new ExceptionRequest
        {
            ExceptionRequestId = 1, RequestCode = $"EXC-{TestFixtures.Today.Year}-001", SourceType = "ResourceConflict",
            ProjectId = 15, AllocationId = pending.AllocationId, RequestedBy = ManagerId,
            RequestNote = "Chờ duyệt", Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var calendar = await _repo.GetCalendarAsync(_eventDate, _eventDate, null, ManagerId);

        Assert.NotNull(calendar);
        var lane = calendar!.Lanes.Single(l => l.ResourceId == AudioResourceId);
        var usage = Assert.Single(lane.Usages);
        Assert.Equal(6, usage.ReservedQuantity);
        Assert.Equal(4, usage.ApprovedQuantity);
        Assert.Equal(3, usage.PendingQuantity);
        Assert.Equal(10, usage.CommittedQuantity);
        Assert.False(usage.HasConflict);
        Assert.Equal(1, calendar.Summary.PendingExceptions);
        Assert.Equal(3, calendar.Summary.Allocations);
    }

    [Fact]
    public async Task GetCalendar_FlagsOverCapacityAllocationsAsConflicts()
    {
        await SeedAsync();
        await AddAllocationAsync(14, quantity: 6);
        await AddAllocationAsync(15, quantity: 5);

        var calendar = await _repo.GetCalendarAsync(_eventDate, _eventDate, null, ManagerId);

        Assert.NotNull(calendar);
        var usage = calendar!.Lanes.Single(l => l.ResourceId == AudioResourceId).Usages.Single();
        Assert.True(usage.HasConflict);
        Assert.Equal(2, calendar.Summary.Conflicts); // both reserved allocations are over capacity
        Assert.All(usage.Allocations, a => Assert.Equal("Reserved", a.Status));
    }

    [Fact]
    public async Task GetCalendar_RejectsARangeLongerThanTwoMonths()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.GetCalendarAsync(TestFixtures.Today, TestFixtures.Today.AddDays(63), null, ManagerId));

        Assert.Contains("tối đa 2 tháng", ex.Message);
    }

    [Fact]
    public async Task GetCalendar_RejectsAnInvertedRange()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.GetCalendarAsync(TestFixtures.Today, TestFixtures.Today.AddDays(-1), null, ManagerId));

        Assert.Contains("Ngày bắt đầu", ex.Message);
    }

    [Fact]
    public async Task GetCalendar_ExcludesAllocationsOfOtherManagers()
    {
        await SeedAsync();
        await AddAllocationAsync(16, quantity: 4);

        var calendar = await _repo.GetCalendarAsync(_eventDate, _eventDate, null, ManagerId);

        Assert.NotNull(calendar);
        Assert.Equal(0, calendar!.Summary.Allocations);
        Assert.All(calendar.Lanes, lane => Assert.Empty(lane.Usages));
    }

    [Fact]
    public async Task GetResources_ReturnsEveryResourceWithItsTypeLabel()
    {
        await SeedAsync();

        var resources = await _repo.GetResourcesAsync();

        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, r => r.ResourceId == AudioResourceId && r.TypeLabel == "Âm thanh");
        Assert.Contains(resources, r => r.ResourceId == LightResourceId && r.TypeLabel == "Đèn follow");
    }
}
