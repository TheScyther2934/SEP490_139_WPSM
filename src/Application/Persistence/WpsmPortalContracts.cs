using Domain.Services;

namespace Application.Persistence;

public sealed record WpsmPortalSnapshot(
    WeddingProjectView? Project,
    IReadOnlyList<WeddingProjectView> Projects,
    IReadOnlyList<CatalogItemView> Catalog,
    IReadOnlyList<PriceFrameView> PriceFrames,
    PolicyView Policies,
    ProfileView Profile)
{
    public int UnreadNotifications { get; init; }
    public int OverdueTasks { get; init; }
}

public sealed record WeddingProjectView(
    string Id,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    decimal BaseValue,
    decimal DepositAmount,
    string Status,
    int CurrentStage,
    int PendingAddons,
    string BeoStatus,
    decimal OutstandingBalance,
    string SpecificationSummary = "Chưa nhập đặc tả",
    int GuestCount = 0,
    IReadOnlyList<SpecificationLineView>? Specification = null,
    DateTime? LockedAt = null,
    string DepositStatus = "Unknown",
    string SlotTime = "",
    int AddonCount = 0)
{
    public string StatusLabel { get; init; } = "";
    public DateOnly LockDate { get; init; }
    public DateOnly SupplierDeadline { get; init; }
    /// <summary>Lowest total allowed in stage 2 (the agreed decrease limit applied to the signed value).</summary>
    public decimal MinimumTotal { get; init; }
    public bool CanAdjust { get; init; }
    public bool CanAddAddon { get; init; }
    public decimal ConfirmedAddonTotal { get; init; }
    public IReadOnlyList<EventStageView> Stages { get; init; } = [];
    public string CustomerEmail { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal CurrentTotal { get; init; }
    public int OpenComplaints { get; init; }
    public int DayOfTotal { get; init; }
    public int DayOfCompleted { get; init; }
    public IReadOnlyList<PaymentView> Payments { get; init; } = [];
    public IReadOnlyList<ProjectModuleItem> Addons { get; init; } = [];
    public IReadOnlyList<ProjectModuleItem> Beos { get; init; } = [];
    public IReadOnlyList<ProjectModuleItem> Complaints { get; init; } = [];
    public IReadOnlyList<ProjectModuleItem> DayOfTasks { get; init; } = [];
    public string CredentialEmailStatus { get; init; } = "ExistingAccount";
}
public sealed record PaymentView(string Type, string Status, decimal Amount, DateOnly? DueDate, DateOnly? PaidDate, bool HasReceipt);
public sealed record ProjectModuleItem(string Title, string Status, string? Detail = null);

public sealed record SpecificationLineView(
    int CatalogItemId,
    string Name,
    string Category,
    string Description,
    int Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record CatalogItemView(string Id, string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameView(string Id, string ServiceName, string Variant, decimal MinPrice, decimal MaxPrice, string Currency, int CatalogItemId = 0);
/// <summary>Event rules Admin can change (PolicyConfigs) plus the hotline shown to customers.</summary>
public sealed record PolicyView(decimal DepositRatio, decimal MaxDecreaseRatio, int BookingMinDays, int BookingMaxDays,
    int LockDays, int SupplierDeadlineDays, int SettlementDueDays, string Hotline);
public sealed record ProfileView(string FullName, string Email, string Phone, string Role);

public sealed record CreateProjectCommand(
    string CoupleName,
    string CustomerEmail,
    string CustomerPhone,
    string Hall,
    string Slot,
    DateOnly EventDate,
    decimal BaseValue,
    decimal DepositAmount,
    string PaymentMethod,
    string? ReceiptFilePath,
    IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CreatedProjectView(WeddingProjectView Project, string? TemporaryPassword);

/// <summary>Stage 2 adjustment: the new service lines and the note explaining what the customer asked for (required).</summary>
public sealed record SpecificationCommand(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null, string? Note = null, string? NoteType = null);
public sealed record SpecificationLineCommand(int CatalogItemId, string Description, int Quantity, decimal UnitPrice);
public sealed record CatalogItemCommand(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameCommand(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyCommand(decimal DepositRatio, decimal MaxDecreaseRatio, int BookingMinDays, int BookingMaxDays,
    int LockDays, int SupplierDeadlineDays, int SettlementDueDays, string? Hotline = null, DateOnly? EffectiveFrom = null);
public sealed record ProfileCommand(string FullName, string Phone);

public sealed record AccountView(int UserId, string Email, string FullName, string? Phone, string Role, string Status, string PasswordHash, bool MustChangePassword);

// ── C-01 Customer Dashboard ─────────────────────────────────────────────────
public sealed record CustomerDashboardView(
    string ProjectId,
    string ProjectCode,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    int CountdownDays,
    string CountdownLabel,
    IReadOnlyList<EventStageView> Stages,
    int PendingAddonCount,
    decimal OutstandingBalance,
    decimal BaseContractValue,
    decimal TotalPaid,
    int CurrentStage,
    DashboardPhotoView? LatestProofPhoto)
{
    public string Status { get; init; } = "";
    public string StatusLabel { get; init; } = "";
    public decimal CurrentTotal { get; init; }
    public IReadOnlyList<OperatorContactView> Contacts { get; init; } = [];
    public string Hotline { get; init; } = "";
}

public sealed record DashboardPhotoView(
    int PhotoId,
    string ImagePath,
    DateTime UploadedAt,
    string UploaderName)
{
    /// <summary>Signed, short-lived URL filled in by the API layer (img tags cannot send a Bearer token).</summary>
    public string ImageUrl { get; init; } = "";
}

public sealed record HallView(int HallId, string Name, int MinCapacity, int MaxCapacity, decimal BasePrice, string Status, IReadOnlyList<SlotView> Slots);
public sealed record SlotView(int SlotId, string SlotName, TimeOnly StartTime, TimeOnly EndTime, bool Available);
