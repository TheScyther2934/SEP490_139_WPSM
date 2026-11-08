using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.DayOfTasks;
using Application.Persistence;
using Infrastructure.DayOfTasks;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Xunit;

namespace Tests;

public sealed class DayOfTaskServiceTests : IDisposable
{
    private sealed class InMemoryFileStorage : IFileStorage
    {
        public readonly Dictionary<string, byte[]> Files = new();

        public Task UploadAsync(Stream content, string key, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Files[key] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Files.Remove(key);
            return Task.CompletedTask;
        }
    }

    private readonly WpsmDbContext _db;
    private readonly InMemoryFileStorage _storage;
    private readonly DayOfTaskService _service;

    private readonly User _manager;
    private readonly User _customer;
    private readonly User _admin;
    private readonly Hall _hall;
    private readonly Slot _slot;
    private readonly WeddingProject _project;

    public DayOfTaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _storage = new InMemoryFileStorage();
        _service = new DayOfTaskService(_db, _storage);

        _manager = new User { UserId = 1, Email = "mgr@example.com", FullName = "Quản Lý", Role = "Manager", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _customer = new User { UserId = 2, Email = "cust@example.com", FullName = "Khách Hàng", Role = "Customer", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _admin = new User { UserId = 3, Email = "admin@example.com", FullName = "Admin", Role = "Admin", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };

        _hall = new Hall { HallId = 1, Name = "Sảnh Kim Cương", MinCapacity = 200, MaxCapacity = 400, BasePrice = 50_000_000, Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Lunch", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(14, 0) };

        _project = new WeddingProject
        {
            ProjectId = 10,
            ProjectCode = "WPSM-2026-OP",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = DateOnly.FromDateTime(DateTime.Today),
            BaseContractValue = 100_000_000,
            Nrdamount = 30_000_000,
            CurrentTotal = 100_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.AddRange(_manager, _customer, _admin);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.WeddingProjects.Add(_project);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetDayOfTasks_WhenManagerOwnsProject_ReturnsTasks()
    {
        _db.DayOfTasks.Add(new DayOfTask
        {
            DayOfTaskId = 1,
            ProjectId = _project.ProjectId,
            TaskName = "Kiểm tra âm thanh",
            DeadlineTime = DateTime.UtcNow.AddHours(2),
            Status = "Pending",
            PhotoRequired = true
        });
        await _db.SaveChangesAsync();

        var tasks = await _service.GetDayOfTasksAsync("WPSM-2026-OP", _manager.UserId, "Manager");

        Assert.NotNull(tasks);
        Assert.Single(tasks);
        Assert.Equal("Kiểm tra âm thanh", tasks[0].TaskName);
    }

    [Fact]
    public async Task GetDayOfTasks_WhenManagerDoesNotOwnProject_ReturnsNull()
    {
        var otherManagerId = 999;
        var tasks = await _service.GetDayOfTasksAsync("WPSM-2026-OP", otherManagerId, "Manager");

        Assert.Null(tasks);
    }

    [Fact]
    public async Task CompleteDayOfTask_WhenPhotoRequiredAndNoPhotos_ReturnsFailureMSG10()
    {
        _db.DayOfTasks.Add(new DayOfTask
        {
            DayOfTaskId = 2,
            ProjectId = _project.ProjectId,
            TaskName = "Yêu cầu ảnh chụp",
            DeadlineTime = DateTime.UtcNow.AddHours(1),
            Status = "Pending",
            PhotoRequired = true
        });
        await _db.SaveChangesAsync();

        var command = new CompleteDayOfTaskCommand(
            TaskId: 2,
            UserId: _manager.UserId,
            UserRole: "Manager",
            Photos: Array.Empty<DayOfTaskUploadFile>());

        var result = await _service.CompleteDayOfTaskAsync(command);

        Assert.False(result.Success);
        Assert.Contains("MSG10", result.Message);
    }

    [Fact]
    public async Task CompleteDayOfTask_WhenPhotoRequiredAndValidPhotoUploaded_Succeeds()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-10); // Overdue
        _db.DayOfTasks.Add(new DayOfTask
        {
            DayOfTaskId = 3,
            ProjectId = _project.ProjectId,
            TaskName = "Nhiệm vụ có minh chứng",
            DeadlineTime = deadline,
            Status = "Overdue",
            PhotoRequired = true
        });
        await _db.SaveChangesAsync();

        var fakeImageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var photoFile = new DayOfTaskUploadFile(
            Stream: new MemoryStream(fakeImageBytes),
            FileName: "proof.jpg",
            ContentType: "image/jpeg",
            Length: fakeImageBytes.Length);

        var command = new CompleteDayOfTaskCommand(
            TaskId: 3,
            UserId: _manager.UserId,
            UserRole: "Manager",
            Photos: [photoFile]);

        var result = await _service.CompleteDayOfTaskAsync(command);

        Assert.True(result.Success);
        Assert.NotNull(result.Task);
        Assert.Equal("Done", result.Task.Status);
        Assert.True(result.Task.IsLate); // Completed after deadline
        Assert.Single(result.Task.Photos);

        // Verify ExecutionPhoto entity created in database
        var dbPhoto = await _db.ExecutionPhotos.FirstOrDefaultAsync(p => p.DayOfTaskId == 3);
        Assert.NotNull(dbPhoto);
        Assert.Equal(_manager.UserId, dbPhoto.UploaderId);
    }
}
