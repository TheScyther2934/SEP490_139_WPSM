using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// FE-05 Change Request — C-05/C-06 (customer list and create), M-07/M-08 (manager list, negotiation,
/// approvals) and the UC-47 penalty-waiver request. Exercises GBR-02 (PriceFrame), GBR-03 (cost),
/// GBR-04 (mutual approval), GBR-05 (Stale BEO) and GBR-13 (append-only history).
/// </summary>
public sealed class ChangeRequestRepositoryTests
{
    private const string ProjectCode = "WPSM-2026-014";
    private const int CustomerId = 1;
    private const int ManagerId = 2;
    private const int AdminId = 3;
    private const int OtherManagerId = 4;

    private readonly WpsmDbContext _db;
    private readonly ChangeRequestRepository _repo;

    public ChangeRequestRepositoryTests()
    {
        _db = TestFixtures.NewDb();
        _repo = new ChangeRequestRepository(_db);
    }

    // ── Seed + helpers ────────────────────────────────────────────────────────

    private async Task SeedAsync(DateOnly? eventDate = null, bool frozen = true, bool seedBeo = true)
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(CustomerId, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(AdminId, "Admin", "admin@test.local", "Admin"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.AddRange(
            TestFixtures.NewCatalogItem(10, "Sảnh Hoàng Gia", "Hall", "Sảnh", 50_000_000m),
            TestFixtures.NewCatalogItem(11, "Thực đơn tiệc 8 món", "Catering", "Bàn", 3_500_000m),
            TestFixtures.NewCatalogItem(12, "Trang trí sân khấu", "Decor", "Gói", 8_000_000m));
        _db.PriceFrames.AddRange(
            TestFixtures.NewFrame(10, 40_000_000m, 60_000_000m),
            TestFixtures.NewFrame(11, 3_000_000m, 4_000_000m),
            TestFixtures.NewFrame(12, 6_000_000m, 10_000_000m));
        _db.WeddingProjects.Add(TestFixtures.NewProject(14, ProjectCode, CustomerId, ManagerId,
            eventDate: eventDate ?? TestFixtures.Today.AddDays(10),
            status: frozen ? "Baseline Frozen" : "Active Baseline",
            baseValue: 100_000_000m,
            frozen: frozen));
        _db.SpecItems.AddRange(
            TestFixtures.NewSpecItem(100, 14, 10, "Sảnh Hoàng Gia - tiệc tối", 1, 50_000_000m),
            TestFixtures.NewSpecItem(101, 14, 11, "Thực đơn tiệc 8 món", 30, 3_500_000m),
            TestFixtures.NewSpecItem(102, 14, 12, "Trang trí sân khấu", 1, 8_000_000m));
        _db.PolicyConfigs.Add(TestFixtures.NewPenaltyRule(1, 0, 14, 5m));
        if (seedBeo) _db.Beos.Add(new Beo { Beoid = 1, ProjectId = 14, Version = 1, Status = "Frozen", FrozenAt = DateTime.UtcNow.AddDays(-2) });
        await _db.SaveChangesAsync();
    }

    private static CrSaveCommand Ticket(
        string type = "Table count",
        string description = "Tăng 5 bàn tiệc theo yêu cầu của gia đình",
        int quantity = 35,
        decimal unitPrice = 3_500_000m,
        bool submit = true) =>
        new(type, description, [new CrLineCommand(101, quantity, unitPrice)], submit);

    private async Task<CrDetailView> CreateSubmittedAsync(CrSaveCommand? command = null) =>
        (await _repo.CreateAsync(ProjectCode, ManagerId, "Manager", command ?? Ticket()))!;

    private async Task<ChangeRequest> TicketRowAsync(string code) =>
        await _db.ChangeRequests.SingleAsync(c => c.Crcode == code);

    // ── C-06 create ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsDraftWithServerCalculatedCost()
    {
        await SeedAsync();

        var detail = await _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(submit: false));

        Assert.NotNull(detail);
        Assert.Equal($"CR-{TestFixtures.Today.Year}-001", detail!.Code);
        Assert.Equal("Draft", detail.Status);
        Assert.Equal(17_500_000m, detail.Cost.PriceDelta);
        Assert.Equal(5m, detail.Cost.PenaltyRate);
        Assert.Equal(875_000m, detail.Cost.PenaltyFee);
        Assert.Equal(18_375_000m, detail.Cost.TotalImpact);
        Assert.Single(detail.Items);
        Assert.Equal(17_500_000m, detail.Items[0].DeltaAmount);
        Assert.Single(detail.History);
        Assert.Equal("Created", detail.History[0].Action);
    }

    [Fact]
    public async Task Create_SubmitMovesTicketToNegotiatingAndNotifiesTheCounterparty()
    {
        await SeedAsync();

        var detail = await CreateSubmittedAsync();

        Assert.Equal("Negotiating", detail.Status);
        Assert.Contains(detail.History, h => h.Action == "Submitted");
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == CustomerId && n.Type == "CR update"));
    }

    [Fact]
    public async Task Create_AppliesTheTdayPenaltyInsideTheConfiguredWindow()
    {
        await SeedAsync(eventDate: TestFixtures.Today.AddDays(10));

        var detail = await CreateSubmittedAsync();

        Assert.Equal(5m, detail.Cost.PenaltyRate);
        Assert.Equal(875_000m, detail.Cost.PenaltyFee);
        Assert.True(detail.Cost.InPenaltyWindow);
        Assert.Equal(10, detail.Cost.DaysToEvent);
    }

    [Fact]
    public async Task Create_AppliesNoPenaltyOutsideTheConfiguredWindow()
    {
        await SeedAsync(eventDate: TestFixtures.Today.AddDays(60));

        var detail = await CreateSubmittedAsync();

        Assert.Equal(0m, detail.Cost.PenaltyRate);
        Assert.Equal(0m, detail.Cost.PenaltyFee);
        Assert.False(detail.Cost.InPenaltyWindow);
        Assert.Equal(17_500_000m, detail.Cost.TotalImpact);
    }

    [Fact]
    public async Task Create_RejectsUnknownChangeType()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(type: "Không tồn tại")));

        Assert.Contains("Loại yêu cầu thay đổi", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsBlankDescription()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(description: "  ")));

        Assert.Contains("Lý do thay đổi", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsDescriptionLongerThan500Characters()
    {
        await SeedAsync();
        var command = Ticket(description: new string('x', 501));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", command));

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsEmptyItemList()
    {
        await SeedAsync();
        var command = new CrSaveCommand("Table count", "Không có hạng mục", []);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", command));

        Assert.Contains("ít nhất một hạng mục", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsMoreThanTwentyItems()
    {
        await SeedAsync();
        var lines = Enumerable.Range(0, 21).Select(_ => new CrLineCommand(101, 35, 3_500_000m)).ToList();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", new CrSaveCommand("Table count", "Quá nhiều dòng", lines)));

        Assert.Contains("tối đa 20", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsNegativeQuantity()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(quantity: -1)));

        Assert.Contains("Số lượng mới", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsDuplicateSpecItemLines()
    {
        await SeedAsync();
        var command = new CrSaveCommand("Table count", "Trùng hạng mục",
            [new CrLineCommand(101, 35, 3_500_000m), new CrLineCommand(101, 36, 3_500_000m)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", command));

        Assert.Contains("một lần", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsPriceOutsideTheCurrentPriceFrame()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(unitPrice: 5_000_000m)));

        Assert.Contains("MSG06", ex.Message);
    }

    [Fact]
    public async Task Create_RejectsSpecItemOutsideTheProjectBaseline()
    {
        await SeedAsync();
        var command = new CrSaveCommand("Table count", "Hạng mục lạ", [new CrLineCommand(999, 1, 1_000_000m)]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", command));

        Assert.Contains("không thuộc baseline", ex.Message);
    }

    [Fact]
    public async Task Create_IsBlockedBeforeTheBaselineIsFrozen()
    {
        await SeedAsync(frozen: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket()));

        Assert.Contains("chưa được đóng băng", ex.Message);
    }

    [Fact]
    public async Task Create_IsBlockedAfterTheEventDate()
    {
        await SeedAsync(eventDate: TestFixtures.Today.AddDays(-1));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket()));

        Assert.Contains("đã diễn ra", ex.Message);
    }

    [Fact]
    public async Task Create_ReturnsNullForAnotherManagersProject()
    {
        await SeedAsync();

        var detail = await _repo.CreateAsync(ProjectCode, OtherManagerId, "Manager", Ticket());

        Assert.Null(detail);
    }

    // ── Draft editing and submission ─────────────────────────────────────────

    [Fact]
    public async Task UpdateDraft_ReplacesLinesAndRecalculatesTheImpact()
    {
        await SeedAsync();
        var draft = await _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(submit: false));

        var updated = await _repo.UpdateDraftAsync(ProjectCode, draft!.Code, ManagerId, "Manager",
            Ticket(quantity: 40, description: "Tăng 10 bàn", submit: false));

        Assert.NotNull(updated);
        Assert.Equal("Draft", updated!.Status);
        Assert.Equal(35_000_000m, updated.Cost.PriceDelta);
        Assert.Equal(1_750_000m, updated.Cost.PenaltyFee);
        Assert.Single(await _db.Critems.Where(i => i.Cr.Crcode == draft.Code).ToListAsync());
        Assert.Contains(updated.History, h => h.Action == "Updated");
    }

    [Fact]
    public async Task UpdateDraft_RejectsATicketThatIsAlreadyNegotiating()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateDraftAsync(ProjectCode, submitted.Code, ManagerId, "Manager", Ticket(quantity: 40)));

        Assert.Contains("Chỉ bản nháp", ex.Message);
    }

    [Fact]
    public async Task UpdateDraft_RejectsAnotherUsersDraft()
    {
        await SeedAsync();
        var draft = await _repo.CreateAsync(null, CustomerId, "Customer", Ticket(submit: false));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateDraftAsync(ProjectCode, draft!.Code, ManagerId, "Manager", Ticket(quantity: 40)));

        Assert.Contains("Chỉ người tạo", ex.Message);
    }

    [Fact]
    public async Task Submit_RequiresAtLeastOneItem()
    {
        await SeedAsync();
        _db.ChangeRequests.Add(new ChangeRequest
        {
            Crid = 500, Crcode = "CR-2026-001", ProjectId = 14, CreatedBy = ManagerId,
            Type = "Other", Description = "Nháp rỗng", Status = "Draft", CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SubmitAsync(ProjectCode, "CR-2026-001", ManagerId, "Manager"));

        Assert.Contains("ít nhất một hạng mục", ex.Message);
    }

    // ── M-08 negotiation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Counter_ResetsApprovalsAndRecalculatesTheProposal()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        var row = await TicketRowAsync(submitted.Code);
        row.CustomerApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var countered = await _repo.CounterAsync(ProjectCode, submitted.Code, ManagerId,
            new CrCounterCommand([new CrLineCommand(101, 32, 3_500_000m)], "Chỉ tăng 2 bàn"));

        Assert.NotNull(countered);
        Assert.Equal(7_000_000m, countered!.Cost.PriceDelta);
        Assert.Null(countered.ManagerApprovedAt);
        Assert.Null(countered.CustomerApprovedAt);
        Assert.Contains(countered.History, h => h.Action == "CounterProposal" && h.Note == "Chỉ tăng 2 bàn");
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == CustomerId));
    }

    [Fact]
    public async Task Counter_RejectsADraftTicket()
    {
        await SeedAsync();
        var draft = await _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(submit: false));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.CounterAsync(ProjectCode, draft!.Code, ManagerId, new CrCounterCommand([new CrLineCommand(101, 32, 3_500_000m)])));

        Assert.Contains("đang thương lượng", ex.Message);
    }

    [Fact]
    public async Task Approve_ManagerOnlyKeepsTheTicketNegotiating()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var after = await _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", "Đồng ý");

        Assert.NotNull(after);
        Assert.Equal("Negotiating", after!.Status);
        Assert.NotNull(after.ManagerApprovedAt);
        Assert.Null(after.CustomerApprovedAt);
        Assert.Contains(after.History, h => h.Action == "ManagerApproved" && h.Note == "Đồng ý");
    }

    [Fact]
    public async Task Approve_BothPartiesCreatesTheAddendumAndUpdatesTheProjectTotal()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        await _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", null);

        var approved = await _repo.ApproveAsync(ProjectCode, submitted.Code, CustomerId, "Customer", null);

        Assert.NotNull(approved);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal($"PL-{TestFixtures.Today.Year}-001", approved.AddendumCode);
        Assert.Contains(approved.History, h => h.Action == "Approved");
        Assert.Contains(approved.History, h => h.Action == "AddendumGenerated");
        var project = await _db.WeddingProjects.SingleAsync(p => p.ProjectId == 14);
        Assert.Equal(118_375_000m, project.CurrentTotal); // 100m + 17.5m delta + 0.875m penalty
        Assert.Equal(18_375_000m, (await _db.ContractAddenda.SingleAsync()).TotalVariance);
    }

    [Fact]
    public async Task Approve_FlagsAFrozenBeoAsStale()
    {
        await SeedAsync(seedBeo: true);
        var submitted = await CreateSubmittedAsync();
        await _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", null);

        await _repo.ApproveAsync(ProjectCode, submitted.Code, CustomerId, "Customer", null);

        Assert.Equal("Stale", (await _db.Beos.SingleAsync()).Status);
    }

    [Fact]
    public async Task Approve_RejectsASecondApprovalByTheSameParty()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        await _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", null));

        Assert.Contains("đã phê duyệt", ex.Message);
    }

    [Fact]
    public async Task Approve_KeepsThePenaltyWaiverGrantedByTheAdmin()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        var row = await TicketRowAsync(submitted.Code);
        _db.ExceptionRequests.Add(new ExceptionRequest
        {
            ExceptionRequestId = 1, RequestCode = $"EXC-{TestFixtures.Today.Year}-001", SourceType = "CrPenaltyWaiver",
            ProjectId = 14, Crid = row.Crid, RequestedBy = ManagerId, RequestNote = "Miễn phạt",
            Status = "Approved", DecisionBy = AdminId, DecisionNote = "Chấp thuận", DecidedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        await _db.SaveChangesAsync();

        await _repo.ApproveAsync(ProjectCode, submitted.Code, ManagerId, "Manager", null);
        var approved = await _repo.ApproveAsync(ProjectCode, submitted.Code, CustomerId, "Customer", null);

        Assert.Equal(0m, approved!.Cost.PenaltyFee);
        Assert.Equal(117_500_000m, (await _db.WeddingProjects.SingleAsync(p => p.ProjectId == 14)).CurrentTotal);
    }

    [Fact]
    public async Task Reject_RequiresAReason()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RejectAsync(ProjectCode, submitted.Code, ManagerId, "Manager", "   "));

        Assert.Contains("Lý do từ chối", ex.Message);
    }

    [Fact]
    public async Task Reject_ClosesTheTicketAndKeepsTheAuditTrail()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var rejected = await _repo.RejectAsync(ProjectCode, submitted.Code, ManagerId, "Manager", "Khách đổi ý");

        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Contains(rejected.History, h => h.Action == "Rejected" && h.Note == "Khách đổi ý");
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == CustomerId));
    }

    // ── UC-47 penalty waiver ─────────────────────────────────────────────────

    [Fact]
    public async Task RequestException_CreatesAPendingWaiverAndHistoryEntry()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var detail = await _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "Sảnh còn trống nên xin miễn phạt");

        Assert.NotNull(detail);
        Assert.Equal("Pending", detail!.PendingException);
        var request = await _db.ExceptionRequests.SingleAsync();
        Assert.Equal("CrPenaltyWaiver", request.SourceType);
        Assert.Equal("Pending", request.Status);
        Assert.Equal(submitted.Code, (await _db.ChangeRequests.SingleAsync(c => c.Crid == request.Crid)).Crcode);
        Assert.Contains(detail.History, h => h.Action == "ExceptionRequested");
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == AdminId));
    }

    [Fact]
    public async Task RequestException_RequiresANote()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "  "));

        Assert.Contains("Ghi chú gửi Admin", ex.Message);
    }

    [Fact]
    public async Task RequestException_IsRejectedWhenTheTicketHasNoPenalty()
    {
        await SeedAsync(eventDate: TestFixtures.Today.AddDays(60));
        var submitted = await CreateSubmittedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "Xin miễn phạt"));

        Assert.Contains("không có phí phạt", ex.Message);
    }

    [Fact]
    public async Task RequestException_RejectsASecondPendingRequest()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        await _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "Lần một");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "Lần hai"));

        Assert.Contains("Đã có yêu cầu ngoại lệ", ex.Message);
    }

    // ── C-05 / M-07 list and M-08 detail ─────────────────────────────────────

    [Fact]
    public async Task GetList_BuildsTheSummaryAndFlagsPendingExceptions()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();
        await _repo.RequestExceptionAsync(ProjectCode, submitted.Code, ManagerId, "Xin miễn phạt");
        var second = await _repo.CreateAsync(ProjectCode, ManagerId, "Manager", Ticket(submit: true));
        await _repo.RejectAsync(ProjectCode, second!.Code, ManagerId, "Manager", "Không cần thiết");

        var list = await _repo.GetListAsync(ProjectCode, ManagerId, "Manager");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Summary.Total);
        Assert.Equal(1, list.Summary.Negotiating);
        Assert.Equal(1, list.Summary.Rejected);
        Assert.Equal(0m, list.Summary.ApprovedImpact);
        Assert.Equal(18_375_000m, list.Summary.PendingImpact);
        Assert.Equal(875_000m, list.Summary.PendingPenalty);
        var pending = list.Items.Single(i => i.Code == submitted.Code);
        Assert.Equal("Pending", pending.PendingException);
    }

    [Fact]
    public async Task GetDetail_ExposesTheCapabilitiesOfEachRole()
    {
        await SeedAsync();
        var submitted = await CreateSubmittedAsync();

        var managerView = await _repo.GetDetailAsync(ProjectCode, submitted.Code, ManagerId, "Manager");
        var customerView = await _repo.GetDetailAsync(ProjectCode, submitted.Code, CustomerId, "Customer");

        Assert.NotNull(managerView);
        Assert.True(managerView!.CanEdit);
        Assert.True(managerView.CanApprove);
        Assert.True(managerView.CanReject);
        Assert.True(managerView.CanRequestException);
        Assert.NotNull(customerView);
        Assert.False(customerView!.CanEdit);
        Assert.True(customerView.CanApprove);
        Assert.True(customerView.CanReject);
        Assert.False(customerView.CanRequestException);
    }

    [Fact]
    public async Task GetList_ReturnsNullForAnotherManagersProject()
    {
        await SeedAsync();

        var list = await _repo.GetListAsync(ProjectCode, OtherManagerId, "Manager");

        Assert.Null(list);
    }
}
