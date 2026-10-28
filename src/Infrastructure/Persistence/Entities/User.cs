using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Beo> Beos { get; set; } = new List<Beo>();

    public virtual ICollection<ChangeRequest> ChangeRequests { get; set; } = new List<ChangeRequest>();

    public virtual ICollection<Crhistory> Crhistories { get; set; } = new List<Crhistory>();

    public virtual ICollection<ExecutionPhoto> ExecutionPhotos { get; set; } = new List<ExecutionPhoto>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<PolicyConfig> PolicyConfigs { get; set; } = new List<PolicyConfig>();

    public virtual ICollection<PriceFrame> PriceFrames { get; set; } = new List<PriceFrame>();

    public virtual ICollection<ResourceAllocation> ResourceAllocations { get; set; } = new List<ResourceAllocation>();

    public virtual ICollection<WeddingProject> WeddingProjectCustomers { get; set; } = new List<WeddingProject>();

    public virtual ICollection<WeddingProject> WeddingProjectManagers { get; set; } = new List<WeddingProject>();
}
