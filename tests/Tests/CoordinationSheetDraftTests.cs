using Application.UseCases.CoordinationSheets;
using Domain.Entities;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>Coordination sheet drafts (M-13, M-14, JOB-03): the Beo behaviour alone, then the use cases on an in-memory database.</summary>
public sealed class CoordinationSheetDraftTests
{
    private const int ManagerId = 2;
    private const int OtherManagerId = 4;
    private readonly WpsmDbContext _db = TestFixtures.NewDb();

    [Fact]
    public void A_draft_takes_the_notes_as_written_and_an_approved_version_keeps_its_notes_when_none_are_given()
    {
        var draft = Beo.NewDraft(10, "Ghi chú đầu");
        draft.EditNotes("  ");
        Assert.Null(draft.Notes);

        var approved = new Beo { Status = "Frozen", Notes = "Đã chốt" };
        approved.EditNotes(null);
        Assert.Equal("Đã chốt", approved.Notes);
        approved.EditNotes(" Thêm lưu ý ");
        Assert.Equal("Thêm lưu ý", approved.Notes);
    }

    [Fact]
    public void Each_version_has_one_sheet_per_department_reissued_in_place()
    {
        var sheet = Beo.NewDraft(10, "x");
        var first = DateTime.UtcNow.AddHours(-1);
        sheet.RefreshDepartmentSheets("WPSM-2026-014", first);
        sheet.RefreshDepartmentSheets("WPSM-2026-014", first.AddHours(1));

        Assert.Equal(["Kitchen", "Service", "Decor", "Tech-MC"], sheet.BeodepartmentSheets.Select(s => s.Department));
        Assert.All(sheet.BeodepartmentSheets, s => Assert.Equal(first.AddHours(1), s.GeneratedAt));
        Assert.Equal("beo/WPSM-2026-014_v1_Kitchen.pdf", sheet.BeodepartmentSheets.First().PdfFilePath);
    }

    private BeoRepository Reads() => new(_db);
    private SaveCoordinationSheetDraft SaveDraft() => new(new CoordinationSheetStore(_db), Reads(), new UnitOfWork(_db));
    private GenerateDepartmentSheets Generate() => new(new CoordinationSheetStore(_db), Reads(), new UnitOfWork(_db));
    private CompileLockedDrafts Compile() =>
        new(new CoordinationSheetStore(_db), new EventMessages(_db, new TestFixtures.RecordingEmailOutbox(), new EmailOptions()), new UnitOfWork(_db));

    private async Task SeedAsync()
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(1, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(OtherManagerId, "Manager", "manager4@test.local", "Quản lý Khác"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.ServiceCatalogItems.Add(TestFixtures.NewCatalogItem(11, "Thực đơn tiệc 8 món", "Catering", "Bàn", 3_500_000m));
        _db.WeddingProjects.AddRange(
            TestFixtures.NewProject(14, "WPSM-2026-014", 1, ManagerId, eventDate: TestFixtures.Today.AddDays(9), status: "Locked", frozen: true),
            TestFixtures.NewProject(15, "WPSM-2026-015", 1, ManagerId, eventDate: TestFixtures.Today.AddDays(9), status: "Adjusting"),
            TestFixtures.NewProject(16, "WPSM-2026-016", 1, ManagerId, eventDate: TestFixtures.Today.AddDays(-2), status: "Locked", frozen: true));
        _db.SpecItems.Add(TestFixtures.NewSpecItem(101, 14, 11, "Thực đơn tiệc 8 món", 30, 3_500_000m));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Saving_notes_creates_the_v1_draft_then_edits_it()
    {
        await SeedAsync();

        var created = await SaveDraft().HandleAsync("WPSM-2026-014", ManagerId, null);
        var edited = await SaveDraft().HandleAsync("WPSM-2026-014", ManagerId, " Bếp lên món lúc 18:30 ");

        Assert.Equal(("Draft", 1), (created!.Status, created.Version));
        Assert.Equal("Bếp lên món lúc 18:30", edited!.Notes);
        Assert.Single(await _db.Beos.ToListAsync());
        Assert.Null(await SaveDraft().HandleAsync("WPSM-2026-014", OtherManagerId, "x"));
    }

    [Fact]
    public async Task Issuing_department_sheets_creates_a_draft_when_none_exists_and_four_sheets()
    {
        await SeedAsync();

        var view = await Generate().HandleAsync("WPSM-2026-014", ManagerId);
        await Generate().HandleAsync("WPSM-2026-014", ManagerId);

        Assert.Equal(4, view!.DepartmentSheets.Count);
        Assert.Equal(4, await _db.BeodepartmentSheets.CountAsync());
        Assert.Single(await _db.Beos.ToListAsync());
    }

    [Fact]
    public async Task The_daily_job_compiles_a_draft_with_sheets_and_tasks_for_upcoming_locked_events_only_once()
    {
        await SeedAsync();

        var compiled = await Compile().HandleAsync();
        var again = await Compile().HandleAsync();

        Assert.Equal((1, 0), (compiled, again));
        var sheet = await _db.Beos.Include(b => b.BeodepartmentSheets).SingleAsync();
        Assert.Equal((14, "Draft", 4), (sheet.ProjectId, sheet.Status, sheet.BeodepartmentSheets.Count));
        Assert.True(await _db.DayOfTasks.AnyAsync(t => t.ProjectId == 14));
        Assert.Contains(_db.Notifications, n => n.RecipientId == ManagerId && n.LinkUrl == "/manager/projects/WPSM-2026-014/beo");
    }
}
