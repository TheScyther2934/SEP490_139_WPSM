using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

public sealed class NotificationRepositoryTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly NotificationRepository _repo;
    private readonly User _user1;
    private readonly User _user2;

    public NotificationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);
        _repo = new NotificationRepository(_db);

        _user1 = new User
        {
            UserId = 1,
            Email = "manager@test.vn",
            FullName = "Nguyễn Văn Quản Lý",
            Role = "Manager",
            Status = "Active",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };

        _user2 = new User
        {
            UserId = 2,
            Email = "customer@test.vn",
            FullName = "Trần Thị Khách",
            Role = "Customer",
            Status = "Active",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.AddRange(_user1, _user2);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetNotifications_OnlyReturnsCurrentUserNotifications()
    {
        // Arrange
        _db.Notifications.AddRange(
            new Notification { RecipientId = 1, Type = NotificationTypes.RoadmapReminder, Message = "Msg 1 for User 1", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 1, Type = NotificationTypes.Payment, Message = "Msg 2 for User 1", IsRead = true, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 2, Type = NotificationTypes.Complaint, Message = "Msg for User 2", IsRead = false, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _repo.GetNotificationsAsync(1, "All");

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.DoesNotContain("User 2", item.Message));
    }

    [Fact]
    public async Task GetNotifications_FilterUnread_ReturnsOnlyUnread()
    {
        // Arrange
        _db.Notifications.AddRange(
            new Notification { RecipientId = 1, Type = NotificationTypes.RoadmapReminder, Message = "Unread 1", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 1, Type = NotificationTypes.Payment, Message = "Read 1", IsRead = true, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 1, Type = NotificationTypes.CrUpdate, Message = "Unread 2", IsRead = false, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var resultAll = await _repo.GetNotificationsAsync(1, "All");
        var resultUnread = await _repo.GetNotificationsAsync(1, "Unread");

        // Assert
        Assert.Equal(3, resultAll.TotalCount);
        Assert.Equal(2, resultAll.UnreadCount);
        Assert.Equal(2, resultUnread.TotalCount);
        Assert.All(resultUnread.Items, item => Assert.False(item.IsRead));
    }

    [Fact]
    public async Task GetNotifications_OrdersByNewestFirst()
    {
        // Arrange
        var t0 = DateTime.UtcNow.AddMinutes(-20);
        var t1 = DateTime.UtcNow.AddMinutes(-10);
        var t2 = DateTime.UtcNow;

        _db.Notifications.AddRange(
            new Notification { RecipientId = 1, Type = NotificationTypes.RoadmapReminder, Message = "Oldest", IsRead = false, CreatedAt = t0 },
            new Notification { RecipientId = 1, Type = NotificationTypes.Payment, Message = "Newest", IsRead = false, CreatedAt = t2 },
            new Notification { RecipientId = 1, Type = NotificationTypes.Beo, Message = "Middle", IsRead = false, CreatedAt = t1 }
        );
        await _db.SaveChangesAsync();

        // Act
        var result = await _repo.GetNotificationsAsync(1, "All");

        // Assert
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Newest", result.Items[0].Message);
        Assert.Equal("Middle", result.Items[1].Message);
        Assert.Equal("Oldest", result.Items[2].Message);
    }

    [Fact]
    public async Task MarkAsRead_SucceedsForOwner_FailsForNonOwner()
    {
        // Arrange
        var n = new Notification
        {
            RecipientId = 1,
            Type = NotificationTypes.RoadmapReminder,
            Message = "Msg",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();

        // Act - non-owner try
        var failResult = await _repo.MarkAsReadAsync(2, n.NotificationId);

        // Act - owner try
        var successResult = await _repo.MarkAsReadAsync(1, n.NotificationId);

        // Assert
        Assert.False(failResult);
        Assert.True(successResult);

        var updated = await _db.Notifications.FindAsync(n.NotificationId);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_OnlyUpdatesCurrentUserUnread()
    {
        // Arrange
        _db.Notifications.AddRange(
            new Notification { RecipientId = 1, Type = NotificationTypes.RoadmapReminder, Message = "User 1 Unread 1", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 1, Type = NotificationTypes.Payment, Message = "User 1 Unread 2", IsRead = false, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 1, Type = NotificationTypes.Beo, Message = "User 1 Already Read", IsRead = true, CreatedAt = DateTime.UtcNow },
            new Notification { RecipientId = 2, Type = NotificationTypes.Complaint, Message = "User 2 Unread", IsRead = false, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        // Act
        var updatedCount = await _repo.MarkAllAsReadAsync(1);

        // Assert
        Assert.Equal(2, updatedCount);

        var user1UnreadCount = await _repo.GetUnreadCountAsync(1);
        var user2UnreadCount = await _repo.GetUnreadCountAsync(2);

        Assert.Equal(0, user1UnreadCount);
        Assert.Equal(1, user2UnreadCount);
    }

    [Fact]
    public async Task CreateNotification_TruncatesMessageOver300Chars_AndNormalizesType()
    {
        // Arrange
        var longMessage = new string('A', 350);

        // Act
        var created = await _repo.CreateNotificationAsync(
            1,
            "ROADMAP REMINDER",
            longMessage,
            "/customer/roadmap"
        );

        // Assert
        Assert.True(created.Message.Length <= 300);
        Assert.EndsWith("...", created.Message);
        Assert.Equal(NotificationTypes.RoadmapReminder, created.Type);
        Assert.Equal("Roadmap", created.RelatedEntityType);
    }
}
