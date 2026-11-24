using Application.Persistence;
using Application.UseCases.CoordinationTasks;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// Coordination tasks (UC-30..UC-32): the rules on their own (policy and the DayOfTask entity, no database),
/// then the use cases end to end on an in-memory database, as the API calls them.
/// </summary>
public sealed class CoordinationTaskTests : IDisposable
{
    private const int CustomerId = 1, ManagerId = 2;
    private const string Code = "WPSM-2026-070";
    private static readonly DateOnly WeddingDay = VietnamTime.GetVietnamToday().AddDays(5);
    private static DateTime VnTimeOnWeddingDay(int hour) => VietnamTime.ConvertVietnamToUtc(WeddingDay, new TimeOnly(hour, 0));

    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly FakeFiles _files = new();

    public CoordinationTaskTests()
    {
        _db.Users.AddRange(TestFixtures.NewUser(CustomerId, "Customer", "khach@test.local", "Cô dâu"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── Rules, no database ──────────────────────────────────────────────────

    [Theory]
    [InlineData(EventStatus.Locked, true)]
    [InlineData(EventStatus.Completed, true)]
    [InlineData(EventStatus.Adjusting, false)]
    [InlineData(EventStatus.Settled, false)]
    [InlineData(EventStatus.Cancelled, false)]
    public void Tasks_are_open_from_the_lock_until_settlement(string status, bool open)
    {
        Assert.Equal(open, CoordinationTaskPolicy.IsOpen(status));
        if (open) CoordinationTaskPolicy.EnsureOpen(status);
        else Assert.Throws<BusinessRuleException>(() => CoordinationTaskPolicy.EnsureOpen(status));
    }

    [Fact]
    public void Details_are_checked_trimmed_and_kept_between_the_lock_and_the_wedding_day()
    {
        var lockedAt = DateTime.UtcNow.AddDays(-3);
        CoordinationTaskDetails Check(string name, string department, DateTime deadline) =>
            CoordinationTaskPolicy.ValidateDetails(name, department, deadline, true, "  ghi chú  ", WeddingDay, lockedAt);

        Assert.Throws<BusinessRuleException>(() => Check("ab", "Decor", VnTimeOnWeddingDay(9)));
        Assert.Throws<BusinessRuleException>(() => Check("Dựng cổng hoa", "Florist", VnTimeOnWeddingDay(9)));
        Assert.Throws<BusinessRuleException>(() => Check("Dựng cổng hoa", "Decor", VnTimeOnWeddingDay(9).AddDays(1)));
        Assert.Throws<BusinessRuleException>(() => Check("Dựng cổng hoa", "Decor", lockedAt.AddDays(-2)));

        var details = Check("  Dựng cổng hoa ", "Decor", VnTimeOnWeddingDay(9));
        Assert.Equal("Dựng cổng hoa", details.TaskName);
        Assert.Equal("ghi chú", details.Note);
        Assert.Equal(DateTimeKind.Utc, details.DeadlineUtc.Kind);
    }

    [Fact]
    public void Moving_an_overdue_deadline_later_clears_the_flag_but_a_done_task_stays_done()
    {
        var task = new DayOfTask { Status = "Overdue", OverdueFlaggedAt = DateTime.UtcNow, SourceKey = "spec:1" };
        task.Edit(new("Dựng sân khấu", "Decor", DateTime.UtcNow.AddHours(5), false, null), DateTime.UtcNow);
        Assert.Equal("Pending", task.Status);
        Assert.Null(task.OverdueFlaggedAt);

        var done = new DayOfTask { Status = "Done" };
        done.Edit(new("Dựng sân khấu", "Decor", DateTime.UtcNow.AddHours(5), false, null), DateTime.UtcNow);
        Assert.Equal("Done", done.Status);
    }

    [Fact]
    public void Only_a_task_added_by_hand_without_proof_can_be_deleted()
    {
        Assert.Throws<BusinessRuleException>(() => new DayOfTask { SourceKey = "spec:1", Status = "Pending" }.EnsureCanDelete());
        Assert.Throws<BusinessRuleException>(() => new DayOfTask { Status = "Done" }.EnsureCanDelete());
        var withProof = new DayOfTask { Status = "Pending" };
        withProof.ExecutionPhotos.Add(new ExecutionPhoto());
        Assert.Throws<BusinessRuleException>(withProof.EnsureCanDelete);
        new DayOfTask { Status = "Pending" }.EnsureCanDelete();
    }

    [Fact]
    public void Completion_takes_at_most_three_files_and_keeps_the_note()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<BusinessRuleException>(() => new DayOfTask { Status = "Pending" }.Complete(ManagerId, now, 4));

        var task = new DayOfTask { Status = "Overdue", DeadlineTime = now.AddHours(-1) };
        task.Complete(ManagerId, now, 0, "  Xong muộn do mưa ");
        Assert.True(task.IsDone);
        Assert.True(task.IsLate);
        Assert.Equal("Xong muộn do mưa", task.Note);
    }

    [Fact]
    public void Proof_added_afterwards_is_one_to_three_files_up_to_six_per_task()
    {
        var task = new DayOfTask();
        Assert.Throws<BusinessRuleException>(() => task.EnsureCanAddProof(0));
        Assert.Throws<BusinessRuleException>(() => task.EnsureCanAddProof(4));
        for (var i = 0; i < 5; i++) task.ExecutionPhotos.Add(new ExecutionPhoto());
        Assert.Throws<BusinessRuleException>(() => task.EnsureCanAddProof(2));
        task.EnsureCanAddProof(1);
    }

    // ── Use cases on a database ─────────────────────────────────────────────

    private void SeedEvent(string status)
    {
        _db.WeddingProjects.Add(TestFixtures.NewProject(70, Code, CustomerId, ManagerId, eventDate: WeddingDay, status: status));
        _db.SaveChanges();
    }

    private DayOfTask SeedTask(bool photoRequired, string? sourceKey = null)
    {
        var task = new DayOfTask { ProjectId = 70, TaskName = "Kiểm tra âm thanh", Department = "Tech-MC", Status = "Pending",
            DeadlineTime = VnTimeOnWeddingDay(15), PhotoRequired = photoRequired, SourceKey = sourceKey };
        _db.DayOfTasks.Add(task);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return task;
    }

    private CoordinationTaskStore Store() => new(_db, _files);
    private DayOfRepository Board() => new(_db);
    private UnitOfWork Commit() => new(_db);
    private static CoordinationTaskCommand NewTask(string name = "Đón đoàn nhà trai") => new(name, "Service", VnTimeOnWeddingDay(10), true, null);
    private static readonly ProofPhotoUpload Jpeg = new([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3], "cong-hoa.jpg");

    [Fact]
    public async Task Adding_a_task_before_the_lock_is_refused_and_after_the_lock_shows_on_the_board()
    {
        SeedEvent(EventStatus.Adjusting);
        var add = new AddCoordinationTask(Store(), Board(), Commit());
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => add.HandleAsync(Code, ManagerId, NewTask()));
        Assert.Contains("sau khi khóa dịch vụ", refused.Message);

        var project = _db.WeddingProjects.Single();
        project.Status = EventStatus.Locked;
        project.LockedAt = DateTime.UtcNow.AddDays(-1);
        _db.SaveChanges();

        var board = await add.HandleAsync(Code, ManagerId, NewTask());
        var task = Assert.Single(board!.Tasks);
        Assert.Equal("Đón đoàn nhà trai", task.TaskName);
        Assert.True(task.IsCustom);
        Assert.Equal(ManagerId, _db.DayOfTasks.AsNoTracking().Single().CreatedBy);
    }

    [Fact]
    public async Task Unknown_event_or_task_is_not_found()
    {
        SeedEvent(EventStatus.Locked);
        Assert.Null(await new AddCoordinationTask(Store(), Board(), Commit()).HandleAsync("WPSM-KHONG-CO", ManagerId, NewTask()));
        Assert.Null(await new DeleteCoordinationTask(Store(), Board(), Commit()).HandleAsync(Code, 999));
    }

    [Fact]
    public async Task A_generated_task_can_be_edited_but_not_deleted()
    {
        SeedEvent(EventStatus.Locked);
        var task = SeedTask(photoRequired: true, sourceKey: "spec:9");

        var board = await new EditCoordinationTask(Store(), Board(), Commit()).HandleAsync(Code, task.DayOfTaskId,
            new("Kiểm tra âm thanh lần 2", "Tech-MC", VnTimeOnWeddingDay(16), false, null));
        Assert.False(Assert.Single(board!.Tasks).PhotoRequired);

        await Assert.ThrowsAsync<BusinessRuleException>(() => new DeleteCoordinationTask(Store(), Board(), Commit()).HandleAsync(Code, task.DayOfTaskId));
        Assert.Equal(1, _db.DayOfTasks.Count());
    }

    [Fact]
    public async Task Completion_without_the_required_proof_saves_nothing_and_with_proof_stores_the_file()
    {
        SeedEvent(EventStatus.Locked);
        var task = SeedTask(photoRequired: true);
        var complete = new CompleteCoordinationTask(Store(), Board(), Commit());

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => complete.HandleAsync(Code, task.DayOfTaskId, ManagerId, [], null));
        Assert.Contains("MSG10", refused.Message);
        _db.ChangeTracker.Clear();
        Assert.Equal("Pending", _db.DayOfTasks.AsNoTracking().Single().Status);

        var board = await complete.HandleAsync(Code, task.DayOfTaskId, ManagerId, [Jpeg], "Đã kiểm tra");
        var done = Assert.Single(board!.Tasks);
        Assert.Equal("Done", done.Status);
        Assert.Single(done.Photos);
        Assert.Equal(1, _files.Stored);
    }

    [Fact]
    public async Task A_refused_file_type_stores_nothing()
    {
        SeedEvent(EventStatus.Locked);
        var task = SeedTask(photoRequired: false);
        var notAnImage = new ProofPhotoUpload("hello"u8.ToArray(), "ghi-chu.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddCoordinationProof(Store(), Board(), Commit()).HandleAsync(Code, task.DayOfTaskId, ManagerId, [Jpeg, notAnImage]));
        Assert.Equal(0, _files.Stored);
        Assert.Equal(0, _db.ExecutionPhotos.Count());
    }

    [Fact]
    public async Task After_settlement_tasks_can_no_longer_change()
    {
        SeedEvent(EventStatus.Settled);
        var task = SeedTask(photoRequired: false);
        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new CompleteCoordinationTask(Store(), Board(), Commit()).HandleAsync(Code, task.DayOfTaskId, ManagerId, [], null));
        Assert.Contains("đã tất toán", refused.Message);
    }

    /// <summary>Stores nothing for real: counts the files and hands back a key.</summary>
    private sealed class FakeFiles : IFileService
    {
        public int Stored { get; private set; }

        public Task<StoredFileView> StoreAsync(int projectId, string purpose, string originalName, byte[] content, int? uploadedBy, CancellationToken cancellationToken = default)
        {
            Stored++;
            return Task.FromResult(new StoredFileView(Guid.NewGuid(), purpose, originalName, "image/jpeg", content.Length, DateTime.UtcNow) { StorageKey = $"test/{Stored}.jpg" });
        }

        public Task<StoredFileView?> UploadAsync(FileUploadCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SignedFileUrl?> GetSignedUrlAsync(Guid fileId, int userId, string? role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileContent?> OpenSignedAsync(Guid fileId, long expires, string signature, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileContent?> OpenAsync(string storageKey, string contentType, string fileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
