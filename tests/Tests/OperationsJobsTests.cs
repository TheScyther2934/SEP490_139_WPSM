using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Jobs;
using Infrastructure.Pdf;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Tests;

/// <summary>
/// JOB-01..JOB-06 of Report 4 IV.3, called directly (Hangfire only schedules them).
/// Each job must act on due projects only and be harmless when run twice.
/// </summary>
public sealed class OperationsJobsTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly DateOnly _today = TodayVn();
    private const int CustomerId = 10, ManagerId = 99;

    public OperationsJobsTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new WpsmDbContext(options);
        _db.Halls.Add(new Hall { HallId = 1, Name = "Sảnh A", Status = "Active" });
        _db.Slots.Add(new Slot { SlotId = 1, HallId = 1, SlotName = "Ca Tối", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(22, 0) });
        _db.Users.AddRange(
            new User { UserId = CustomerId, Email = "c@test.vn", FullName = "Khách hàng A", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow },
            new User { UserId = ManagerId, Email = "mgr@test.vn", FullName = "Manager", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private WeddingProject Project(string code, int daysToEvent, string status = "Baseline Frozen")
    {
        var project = new WeddingProject
        {
            ProjectCode = code, CustomerId = CustomerId, ManagerId = ManagerId, HallId = 1, SlotId = 1,
            EventDate = _today.AddDays(daysToEvent), BaseContractValue = 100_000_000m, Nrdamount = 30_000_000m,
            CurrentTotal = 100_000_000m, Status = status, CreatedAt = DateTime.UtcNow
        };
        project.Payments.Add(new Payment { PaymentType = "NRD", Status = "Paid", Amount = 30_000_000m, PaidDate = _today.AddDays(-100) });
        _db.WeddingProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    private OperationsJobs Jobs()
    {
        // The job paths never touch files or PDFs; those dependencies stay unset.
        var outbox = new EmailOutbox(_db);
        return new OperationsJobs(_db, new BeoRepository(_db, null!, null!), new DayOfRepository(_db, null!),
            new PaymentRepository(_db, null!, null!, outbox), outbox, NullLogger<OperationsJobs>.Instance);
    }

    private int NotificationsFor(int userId) => _db.Notifications.Count(x => x.RecipientId == userId);

    [Fact]
    public async Task Job01_RemindsTasksDueWithinThreeDays_OncePerDay()
    {
        var project = Project("JOB-01", 60);
        _db.RoadmapTasks.AddRange(
            new RoadmapTask { ProjectId = project.ProjectId, Milestone = "T-90", Title = "Chốt thực đơn", AssigneeRole = "Customer", DueDate = _today.AddDays(2), Status = "Pending" },
            new RoadmapTask { ProjectId = project.ProjectId, Milestone = "T-90", Title = "Kiểm tra sảnh", AssigneeRole = "Manager", DueDate = _today.AddDays(1), Status = "Pending" },
            new RoadmapTask { ProjectId = project.ProjectId, Milestone = "T-30", Title = "Còn xa", AssigneeRole = "Customer", DueDate = _today.AddDays(10), Status = "Pending" },
            new RoadmapTask { ProjectId = project.ProjectId, Milestone = "T-90", Title = "Đã xong", AssigneeRole = "Customer", DueDate = _today, Status = "Done" });
        _db.SaveChanges();

        await Jobs().RoadmapRemindersAsync(CancellationToken.None);
        await Jobs().RoadmapRemindersAsync(CancellationToken.None);

        Assert.Equal(1, NotificationsFor(CustomerId));
        Assert.Equal(1, NotificationsFor(ManagerId));
        Assert.Single(_db.EmailOutbox);
        Assert.Equal(2, _db.RoadmapTasks.Count(x => x.RemindedAt != null));
    }

    [Fact]
    public async Task Job02_IssuesT30AsTopUpToCumulativeRatio()
    {
        Project("JOB-02", 25);
        Project("JOB-02-FAR", 45);

        await Jobs().T30PaymentsAsync(CancellationToken.None);
        await Jobs().T30PaymentsAsync(CancellationToken.None);

        var t30 = Assert.Single(_db.Payments, x => x.PaymentType == "T30");
        Assert.Equal("Due", t30.Status);
        Assert.Equal(40_000_000m, t30.Amount); // 70% of 100M minus 30M NRD already paid (L-12).
        Assert.Equal(_today, t30.DueDate);
        Assert.Single(_db.EmailOutbox);
    }

    [Fact]
    public async Task Job03_CreatesBeoDraftAtSevenDays_Once()
    {
        Project("JOB-03", 7);
        Project("JOB-03-FAR", 8);

        await Jobs().BeoDraftsAsync(CancellationToken.None);
        await Jobs().BeoDraftsAsync(CancellationToken.None);

        var beo = Assert.Single(_db.Beos);
        Assert.Equal("Draft", beo.Status);
        Assert.Equal(1, beo.Version);
        Assert.Equal(1, NotificationsFor(ManagerId));
    }

    [Fact]
    public async Task Job04_LoadsDayOfTimelineForTodaysEvent_Once()
    {
        var project = Project("JOB-04", 0);
        Project("JOB-04-TOMORROW", 1);

        await Jobs().LoadDayOfTimelineAsync(CancellationToken.None);
        await Jobs().LoadDayOfTimelineAsync(CancellationToken.None);

        var tasks = _db.DayOfTasks.Where(x => x.ProjectId == project.ProjectId).ToList();
        Assert.Equal(DayOfTemplate.Length,_db.DayOfTasks.Count());
        Assert.All(tasks, x => Assert.Equal("Pending", x.Status));
        var start = VnToUtc(project.EventDate, new TimeOnly(18, 0));
        Assert.Contains(tasks, x => x.DeadlineTime == start.AddMinutes(-120));
        Assert.Contains(tasks, x => x.DeadlineTime == start && x.PhotoRequired == false);
    }

    [Fact]
    public async Task Job05_FlagsOnlyPendingTasksPastDeadline()
    {
        var project = Project("JOB-05", 0);
        _db.DayOfTasks.AddRange(
            new DayOfTask { ProjectId = project.ProjectId, TaskName = "Trễ", DeadlineTime = DateTime.UtcNow.AddMinutes(-5), Status = "Pending" },
            new DayOfTask { ProjectId = project.ProjectId, TaskName = "Chưa đến hạn", DeadlineTime = DateTime.UtcNow.AddMinutes(30), Status = "Pending" },
            new DayOfTask { ProjectId = project.ProjectId, TaskName = "Đã xong", DeadlineTime = DateTime.UtcNow.AddMinutes(-30), Status = "Done", CompletedAt = DateTime.UtcNow });
        _db.SaveChanges();

        await Jobs().OverdueDetectionAsync(CancellationToken.None);
        await Jobs().OverdueDetectionAsync(CancellationToken.None);

        Assert.Equal("Overdue", _db.DayOfTasks.Single(x => x.TaskName == "Trễ").Status);
        Assert.Equal("Pending", _db.DayOfTasks.Single(x => x.TaskName == "Chưa đến hạn").Status);
        Assert.Equal("Done", _db.DayOfTasks.Single(x => x.TaskName == "Đã xong").Status);
        Assert.Equal(1, NotificationsFor(ManagerId));
    }

    [Fact]
    public async Task Job06_MovesPastEventsToCompletedWithFinalDue()
    {
        var past = Project("JOB-06", -1);
        var upcoming = Project("JOB-06-NEXT", 3);

        await Jobs().FinalSettlementAsync(CancellationToken.None);
        await Jobs().FinalSettlementAsync(CancellationToken.None);

        Assert.Equal("Event Completed", _db.WeddingProjects.Single(x => x.ProjectId == past.ProjectId).Status);
        Assert.Equal("Baseline Frozen", _db.WeddingProjects.Single(x => x.ProjectId == upcoming.ProjectId).Status);
        var final = Assert.Single(_db.Payments, x => x.PaymentType == "FINAL");
        Assert.Equal(70_000_000m, final.Amount);
        Assert.Equal("Due", final.Status);
        Assert.Equal(1, NotificationsFor(ManagerId));
    }

    [Fact]
    public void Pdf_RendersVietnameseText()
    {
        var bytes = new QuestPdfGenerator().Render(new PdfDocumentModel("Phụ lục hợp đồng – Thay đổi đặc tả", "Dự án WPSM-0001 · Tiệc cưới",
            [("Khách hàng", "Nguyễn Thị Hồng Nhung"), ("Ngày tiệc", "15/06/2027"), ("Phí phạt", "1.250.000 ₫")],
            [new PdfTable("Hạng mục thay đổi", ["Hạng mục", "Số lượng", "Đơn giá"], [["Bàn tiệc cao cấp", "30 → 35", "4.500.000 ₫"]])],
            ["Ghi chú: phí được tính theo GBR-03, đã gồm thuế giá trị gia tăng."]));

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        var path = Environment.GetEnvironmentVariable("WPSM_PDF_SAMPLE");
        if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, bytes);
    }
}
