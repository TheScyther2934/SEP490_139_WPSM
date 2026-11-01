namespace WPSM.Application.Persistence;

public sealed record WpsmPortalSnapshot(
    WeddingProjectView? Project,
    IReadOnlyList<WeddingProjectView> Projects,
    IReadOnlyList<CatalogItemView> Catalog,
    IReadOnlyList<PriceFrameView> PriceFrames,
    PolicyView Policies,
    ProfileView Profile,
    IReadOnlyList<RoadmapTaskView> Roadmap)
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
    decimal NrdAmount,
    string Status,
    int RoadmapProgress,
    int OpenChangeRequests,
    string BeoStatus,
    decimal OutstandingBalance,
    string SpecificationSummary = "Chưa nhập đặc tả",
    int GuestCount = 0,
    IReadOnlyList<SpecificationLineView>? Specification = null)
{
    public string CustomerEmail { get; init; } = "";
    public string CustomerPhone { get; init; } = "";
    public decimal CurrentTotal { get; init; }
    public DateTime? BaselineFrozenAt { get; init; }
    public int OpenComplaints { get; init; }
    public int DayOfTotal { get; init; }
    public int DayOfCompleted { get; init; }
    public IReadOnlyList<PaymentView> Payments { get; init; } = [];
    public IReadOnlyList<ProjectModuleItem> Changes { get; init; } = [];
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
public sealed record PriceFrameView(string Id, string ServiceName, string Variant, decimal MinPrice, decimal MaxPrice, string Currency);
public sealed record PolicyView(decimal NrdRatio, decimal T30Ratio, decimal FinalRatio, int CrPenaltyDays, decimal CrPenaltyPercent);
public sealed record ProfileView(string FullName, string Email, string Phone, string Role);
public sealed record RoadmapTaskView(string Milestone, string Title, string AssigneeRole, DateOnly DueDate, string Status);

public sealed record CreateProjectCommand(
    string CoupleName,
    string CustomerEmail,
    string CustomerPhone,
    string Hall,
    string Slot,
    DateOnly EventDate,
    decimal BaseValue,
    decimal NrdAmount,
    string PaymentMethod,
    string? ReceiptFilePath,
    string? ProtectedCredentialPayload = null);
public sealed record CreatedProjectView(WeddingProjectView Project, string? TemporaryPassword);

public sealed record SpecificationCommand(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record SpecificationLineCommand(int CatalogItemId, string Description, int Quantity, decimal UnitPrice);
public sealed record CatalogItemCommand(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameCommand(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyCommand(decimal NrdRatio, decimal T30Ratio, int CrPenaltyDays, decimal CrPenaltyPercent, DateOnly? EffectiveFrom = null);
public sealed record ProfileCommand(string FullName, string Phone);

public sealed record AccountView(int UserId, string Email, string FullName, string? Phone, string Role, string Status, string PasswordHash, bool MustChangePassword);

public sealed record HallView(int HallId, string Name, int MinCapacity, int MaxCapacity, decimal BasePrice, string Status, IReadOnlyList<SlotView> Slots);
public sealed record SlotView(int SlotId, string SlotName, TimeOnly StartTime, TimeOnly EndTime, bool Available);

