using System;
using Infrastructure.Persistence.Entities;
using Xunit;

namespace Tests;

public sealed class DayOfTaskTests
{
    [Fact]
    public void MarkOverdue_WhenPendingAndExceedsDeadline_TransitionsToOverdueAndRaisesEvent()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-30);
        var task = new DayOfTask
        {
            DayOfTaskId = 10,
            ProjectId = 100,
            TaskName = "Kiểm tra bàn tiệc",
            DeadlineTime = deadline,
            Status = "Pending"
        };

        var now = DateTime.UtcNow;
        var transitioned = task.MarkOverdue(now);

        Assert.True(transitioned);
        Assert.Equal("Overdue", task.Status);
        Assert.Single(task.DomainEvents);
        var ev = System.Linq.Enumerable.First(task.DomainEvents);
        Assert.Equal(10, ev.TaskId);
        Assert.Equal(100, ev.ProjectId);
        Assert.Equal(deadline, ev.DeadlineTime);
        Assert.Equal(now, ev.OverdueAt);
    }

    [Fact]
    public void MarkOverdue_WhenAlreadyDone_ReturnsFalseAndPreservesDoneStatus()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-30);
        var task = new DayOfTask
        {
            DayOfTaskId = 11,
            ProjectId = 100,
            TaskName = "Đón tiếp khách",
            DeadlineTime = deadline,
            Status = "Done",
            CompletedAt = deadline.AddMinutes(-5)
        };

        var transitioned = task.MarkOverdue(DateTime.UtcNow);

        Assert.False(transitioned);
        Assert.Equal("Done", task.Status);
        Assert.Empty(task.DomainEvents);
    }

    [Fact]
    public void MarkOverdue_WhenAlreadyOverdue_ReturnsFalseAndDoesNotRaiseDuplicateEvent()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-60);
        var task = new DayOfTask
        {
            DayOfTaskId = 12,
            ProjectId = 100,
            TaskName = "Giám sát âm thanh",
            DeadlineTime = deadline,
            Status = "Pending"
        };

        var first = task.MarkOverdue(DateTime.UtcNow.AddMinutes(-20));
        Assert.True(first);
        Assert.Single(task.DomainEvents);

        var second = task.MarkOverdue(DateTime.UtcNow);
        Assert.False(second);
        Assert.Single(task.DomainEvents);
    }

    [Fact]
    public void MarkOverdue_WhenBeforeDeadline_ReturnsFalse()
    {
        var deadline = DateTime.UtcNow.AddMinutes(30);
        var task = new DayOfTask
        {
            DayOfTaskId = 13,
            ProjectId = 100,
            TaskName = "Chưa đến hạn",
            DeadlineTime = deadline,
            Status = "Pending"
        };

        var transitioned = task.MarkOverdue(DateTime.UtcNow);

        Assert.False(transitioned);
        Assert.Equal("Pending", task.Status);
        Assert.Empty(task.DomainEvents);
    }

    [Fact]
    public void Complete_WhenPendingOnTime_SetsStatusDoneAndIsLateFalse()
    {
        var deadline = DateTime.UtcNow.AddHours(1);
        var task = new DayOfTask
        {
            DayOfTaskId = 14,
            ProjectId = 100,
            TaskName = "Hoàn thành đúng hạn",
            DeadlineTime = deadline,
            Status = "Pending",
            PhotoRequired = false
        };

        var now = DateTime.UtcNow;
        task.Complete(userId: 5, nowUtc: now, photoCount: 0);

        Assert.Equal("Done", task.Status);
        Assert.Equal(now, task.CompletedAt);
        Assert.False(task.IsLate);
    }

    [Fact]
    public void Complete_WhenOverdue_SetsStatusDoneAndIsLateTrue()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-45);
        var task = new DayOfTask
        {
            DayOfTaskId = 15,
            ProjectId = 100,
            TaskName = "Hoàn thành muộn",
            DeadlineTime = deadline,
            Status = "Overdue",
            PhotoRequired = false
        };

        var now = DateTime.UtcNow;
        task.Complete(userId: 5, nowUtc: now, photoCount: 0);

        Assert.Equal("Done", task.Status);
        Assert.Equal(now, task.CompletedAt);
        Assert.True(task.IsLate);
    }

    [Fact]
    public void Complete_WhenPhotoRequiredAndZeroPhotos_ThrowsException()
    {
        var task = new DayOfTask
        {
            DayOfTaskId = 16,
            ProjectId = 100,
            TaskName = "Yêu cầu ảnh",
            DeadlineTime = DateTime.UtcNow.AddHours(1),
            Status = "Pending",
            PhotoRequired = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            task.Complete(userId: 5, nowUtc: DateTime.UtcNow, photoCount: 0));

        Assert.Contains("yêu cầu từ 1 đến 3 ảnh minh chứng", ex.Message);
    }

    [Fact]
    public void Complete_WhenPhotoRequiredAndFourPhotos_ThrowsException()
    {
        var task = new DayOfTask
        {
            DayOfTaskId = 17,
            ProjectId = 100,
            TaskName = "Yêu cầu tối đa 3 ảnh",
            DeadlineTime = DateTime.UtcNow.AddHours(1),
            Status = "Pending",
            PhotoRequired = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            task.Complete(userId: 5, nowUtc: DateTime.UtcNow, photoCount: 4));

        Assert.Contains("tối đa 3 ảnh minh chứng", ex.Message);
    }

    [Fact]
    public void Complete_WhenAlreadyDone_ThrowsException()
    {
        var task = new DayOfTask
        {
            DayOfTaskId = 18,
            ProjectId = 100,
            TaskName = "Đã hoàn thành trước",
            DeadlineTime = DateTime.UtcNow,
            Status = "Done",
            CompletedAt = DateTime.UtcNow
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            task.Complete(userId: 5, nowUtc: DateTime.UtcNow, photoCount: 1));

        Assert.Contains("đã được hoàn thành trước đó", ex.Message);
    }
}
