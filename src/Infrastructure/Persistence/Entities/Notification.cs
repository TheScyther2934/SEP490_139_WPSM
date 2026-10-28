using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int RecipientId { get; set; }

    public string Type { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Recipient { get; set; } = null!;
}
