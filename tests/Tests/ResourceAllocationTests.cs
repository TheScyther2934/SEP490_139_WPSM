using Application.Persistence;
using Application.UseCases.Resources;
using Domain.Entities;
using Domain.Rules;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// Resource allocation (UC-25, UC-26, UC-47, GBR-09): the rules alone first, then the use cases on an in-memory database.
/// </summary>
public sealed class ResourceAllocationTests
{
    private const int ManagerId = 2;
    private const int OtherManagerId = 4;
    private const int AudioId = 1;
    private const int LightId = 2;

    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly DateOnly _eventDate = TestFixtures.Today.AddDays(10);

    // ── Rules without a database ────────────────────────────────────────────

    [Fact]
    public void A_request_needs_a_positive_quantity_within_the_total_and_an_end_after_the_start()
    {
        var start = new DateTime(2026, 12, 1, 3, 0, 0, DateTimeKind.Utc);
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.ValidateRequest(0, start, start.AddHours(1)));
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.ValidateRequest(1, start, start));
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.EnsureWithinTotal(11, 10));
        ResourceAllocationPolicy.ValidateRequest(1, start, start.AddHours(1));
        ResourceAllocationPolicy.EnsureWithinTotal(10, 10);
    }

    [Fact]
    public void Capacity_counts_reserved_and_approved_allocations_and_adjacent_ranges_do_not_overlap()
    {
        Assert.Equal(0, ResourceAllocationPolicy.Shortage(10, 6, 4));
        Assert.Equal(3, ResourceAllocationPolicy.Shortage(10, 8, 5));
        Assert.True(ResourceAllocationPolicy.CountsAgainstCapacity("Reserved"));
        Assert.True(ResourceAllocationPolicy.CountsAgainstCapacity("Exception Approved"));
        Assert.False(ResourceAllocationPolicy.CountsAgainstCapacity("PendingException"));
        Assert.False(ResourceAllocationPolicy.CountsAgainstCapacity("Released"));
        var noon = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(ResourceAllocationPolicy.Overlaps(noon.AddHours(-2), noon, noon, noon.AddHours(2)));
        Assert.True(ResourceAllocationPolicy.Overlaps(noon.AddHours(-2), noon.AddMinutes(1), noon, noon.AddHours(2)));
    }

    [Fact]
    public void Notes_to_the_admin_are_required_and_at_most_300_characters()
    {
        Assert.Equal("Xin phép", ResourceAllocationPolicy.ValidateNote("  Xin phép ", "Ghi chú"));
        Assert.Contains("Ghi chú quyết định", Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.ValidateNote(" ", "Ghi chú quyết định")).Message);
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.ValidateNote(new string('n', 301), "Ghi chú"));
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.EnsureEventOpen("Settled"));
        Assert.Throws<BusinessRuleException>(() => ResourceAllocationPolicy.EnsureEventOpen("Closed"));
        ResourceAllocationPolicy.EnsureEventOpen("Locked");
    }

    [Fact]
    public void An_allocation_is_not_edited_once_released_or_while_the_admin_decides()
    {
        Assert.Throws<BusinessRuleException>(() => new ResourceAllocation { Status = "Released" }.EnsureCanEdit(false));
        Assert.Throws<BusinessRuleException>(() => new ResourceAllocation { Status = "Reserved" }.EnsureCanEdit(true));
        new ResourceAllocation { Status = "Reserved" }.EnsureCanEdit(false);

    }

    [Fact]
    public void The_admin_decides_once_approving_lets_the_allocation_exceed_and_rejecting_frees_it()
    {
        var now = DateTime.UtcNow;
        var approved = new ExceptionRequest { Status = "Pending", Allocation = new ResourceAllocation { Status = "PendingException" } };
        approved.Decide(true, 3, "Cho phép", now);
        Assert.Equal(("Approved", "Exception Approved", true, "Cho phép"),
            (approved.Status, approved.Allocation.Status, approved.Allocation.IsExceptionApproved, approved.Allocation.ExceptionNote));
        Assert.Throws<BusinessRuleException>(() => approved.Decide(false, 3, "Đổi ý", now));

        var rejected = new ExceptionRequest { Status = "Pending", Allocation = new ResourceAllocation { Status = "Reserved" } };
        rejected.Decide(false, 3, "Không được", now);
        Assert.Equal(("Rejected", "Released"), (rejected.Status, rejected.Allocation.Status));
    }

    // ── Use cases on a database ─────────────────────────────────────────────

    private ResourceAllocationStore Store() => new(_db);
    private ResourceRepository Reads() => new(_db);
    private AllocateResource Allocate() => new(Store(), Reads(), new UnitOfWork(_db));
    private UpdateResourceAllocation Update() => new(Store(), Reads(), new UnitOfWork(_db));
    private ReleaseResourceAllocation Release() => new(Store(), new UnitOfWork(_db));
    private RequestResourceException AskAdmin() => new(Store(), Reads(), new UnitOfWork(_db));

    private async Task SeedAsync()
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(1, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.Resources.AddRange(
            new Resource { ResourceId = AudioId, Name = "Dàn âm thanh JBL", Type = "Audio", TotalQuantity = 10 },
            new Resource { ResourceId = LightId, Name = "Đèn follow", Type = "Follow Light", TotalQuantity = 4 });
        _db.WeddingProjects.AddRange(
            TestFixtures.NewProject(14, "WPSM-2026-014", 1, ManagerId, eventDate: _eventDate, status: "Locked", frozen: true),
            TestFixtures.NewProject(15, "WPSM-2026-015", 1, ManagerId, eventDate: _eventDate, status: "Locked", frozen: true),
            TestFixtures.NewProject(16, "WPSM-2026-016", 1, OtherManagerId, eventDate: _eventDate, status: "Locked", frozen: true),
            TestFixtures.NewProject(17, "WPSM-2026-017", 1, ManagerId, eventDate: _eventDate, status: "Settled", frozen: true));
        await _db.SaveChangesAsync();
    }

    private DateTime At(int hour) => DateTime.SpecifyKind(_eventDate.ToDateTime(new TimeOnly(hour, 0)), DateTimeKind.Utc);

    private AllocationCommand At(string project = "WPSM-2026-014", int resourceId = AudioId, int quantity = 4, int from = 10, int to = 13) =>
        new(resourceId, project, At(from), At(to), quantity);

    [Fact]
    public async Task Allocating_reserves_the_resource_in_the_hall_of_the_event()
    {
        await SeedAsync();

        var result = await Allocate().HandleAsync(ManagerId, At(quantity: 6));

        Assert.Equal(("Reserved", 6, "Đã phân bổ tài nguyên."), (result!.Allocation!.Status, result.Allocation.Quantity, result.Message));
        var saved = await _db.ResourceAllocations.SingleAsync();
        Assert.Equal((14, 1), (saved.ProjectId, saved.HallId));
    }

    [Fact]
    public async Task An_allocation_over_capacity_returns_the_clashes_and_saves_nothing()
    {
        await SeedAsync();
        await Allocate().HandleAsync(ManagerId, At("WPSM-2026-015", quantity: 8));

        var result = await Allocate().HandleAsync(ManagerId, At(quantity: 5));

        Assert.Null(result!.Allocation);
        Assert.Contains("MSG08", result.Message);
        Assert.Equal("WPSM-2026-015", Assert.Single(result.Conflicts).ProjectCode);
        Assert.Single(await _db.ResourceAllocations.ToListAsync());
    }

    [Fact]
    public async Task Adjacent_time_ranges_do_not_clash()
    {
        await SeedAsync();
        await Allocate().HandleAsync(ManagerId, At("WPSM-2026-015", quantity: 8, from: 13, to: 16));

        var result = await Allocate().HandleAsync(ManagerId, At(quantity: 6, from: 10, to: 13));

        Assert.NotNull(result!.Allocation);
    }

    [Fact]
    public async Task Allocating_checks_the_request_the_event_and_who_operates_it()
    {
        await SeedAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Allocate().HandleAsync(ManagerId, At(quantity: 0)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Allocate().HandleAsync(ManagerId, At(quantity: 11)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Allocate().HandleAsync(ManagerId, At(from: 14, to: 12)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Allocate().HandleAsync(ManagerId, At(resourceId: 99)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Allocate().HandleAsync(ManagerId, At("WPSM-2026-017")));
        Assert.Null(await Allocate().HandleAsync(ManagerId, At("WPSM-2026-016")));
        Assert.Empty(await _db.ResourceAllocations.ToListAsync());
    }

    [Fact]
    public async Task Rescheduling_checks_capacity_again_without_counting_the_allocation_itself()
    {
        await SeedAsync();
        var mine = (await Allocate().HandleAsync(ManagerId, At(quantity: 4)))!.Allocation!;
        await Allocate().HandleAsync(ManagerId, At("WPSM-2026-015", quantity: 4));

        var moved = await Update().HandleAsync(ManagerId, mine.AllocationId, At(quantity: 6, from: 11, to: 14));
        var tooMany = await Update().HandleAsync(ManagerId, mine.AllocationId, At(quantity: 7));

        Assert.Equal((6, "Đã cập nhật phân bổ."), (moved!.Allocation!.Quantity, moved.Message));
        Assert.Null(tooMany!.Allocation);
        Assert.Equal(6, (await _db.ResourceAllocations.AsNoTracking().SingleAsync(a => a.AllocationId == mine.AllocationId)).AllocatedQuantity);
        Assert.Null(await Update().HandleAsync(OtherManagerId, mine.AllocationId, At(quantity: 2)));
    }

    [Fact]
    public async Task A_released_allocation_or_one_waiting_for_the_admin_cannot_be_rescheduled()
    {
        await SeedAsync();
        var released = (await Allocate().HandleAsync(ManagerId, At(quantity: 2)))!.Allocation!;
        Assert.True(await Release().HandleAsync(ManagerId, released.AllocationId));
        await Allocate().HandleAsync(ManagerId, At("WPSM-2026-015", quantity: 8));
        var waiting = (await AskAdmin().HandleAsync(ManagerId, At(quantity: 5), "Cần thêm loa"))!.Allocation!;

        Assert.Contains("giải phóng", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update().HandleAsync(ManagerId, released.AllocationId, At(quantity: 1)))).Message);
        Assert.Contains("chờ Admin", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update().HandleAsync(ManagerId, waiting.AllocationId, At(quantity: 1)))).Message);
        Assert.False(await Release().HandleAsync(OtherManagerId, waiting.AllocationId));
    }

    [Fact]
    public async Task Asking_the_admin_saves_the_allocation_with_a_pending_request_that_does_not_count_against_capacity()
    {
        await SeedAsync();
        await Allocate().HandleAsync(ManagerId, At("WPSM-2026-015", quantity: 8));

        var asked = await AskAdmin().HandleAsync(ManagerId, At(quantity: 5), "  Không còn dàn nào khác ");

        Assert.Equal(("Đã gửi yêu cầu ngoại lệ tới Admin.", "Pending"), (asked!.Message, asked.Allocation!.ExceptionStatus));
        var request = await _db.ExceptionRequests.AsNoTracking().FirstAsync(r => r.AllocationId == asked.Allocation.AllocationId);
        Assert.Equal(("EXC-0001", "Không còn dàn nào khác", ManagerId, "ResourceConflict"), (request.RequestCode, request.RequestNote, request.RequestedBy, request.SourceType));
        // The waiting allocation is ignored: 8 + 2 still fits in 10.
        Assert.NotNull((await Allocate().HandleAsync(ManagerId, At(quantity: 2)))!.Allocation);
    }

    [Fact]
    public async Task Asking_the_admin_needs_a_note_and_a_real_conflict()
    {
        await SeedAsync();

        Assert.Contains("Ghi chú", (await Assert.ThrowsAsync<BusinessRuleException>(() => AskAdmin().HandleAsync(ManagerId, At(quantity: 5), " "))).Message);
        Assert.Contains("phân bổ trực tiếp", (await Assert.ThrowsAsync<BusinessRuleException>(() => AskAdmin().HandleAsync(ManagerId, At(quantity: 5), "Xin"))).Message);
        Assert.Empty(await _db.ExceptionRequests.ToListAsync());
    }
}
