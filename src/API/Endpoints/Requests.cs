using Application.Persistence;

namespace API.Endpoints;

// Request/response bodies of the WPSM API. This namespace takes precedence over the older,
// differently shaped records with the same names in API (WpsmPortalStore.cs).
public sealed record CreateProjectRequest(string CoupleName, string CustomerEmail, string CustomerPhone, string Hall, string Slot,
    DateOnly EventDate, decimal BaseValue, decimal DepositAmount, string PaymentMethod = "Cash", string? ReceiptFilePath = null,
    IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CreatedProjectResponse(WeddingProjectView Project, string? TemporaryPassword, string EmailStatus = "ExistingAccount");
public sealed record EmailStatusRequest(string? MessageId, string? Status, string? Error);
public sealed record SpecificationRequest(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null, string? Note = null, string? NoteType = null);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyRequest(decimal DepositRatio, decimal MaxDecreaseRatio, int BookingMinDays, int BookingMaxDays, int LockDays,
    int SupplierDeadlineDays, int SettlementDueDays, string? Hotline = null, DateOnly? EffectiveFrom = null);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record ResolveComplaintRequest(string Decision);
public sealed record ExceptionDecisionRequest(bool Approve, string Note);

// Event stage flow: stage notes and addon contracts.
public sealed record StageNoteRequest(string NoteType, string Content);
public sealed record AddonRequest(string Title, string? Note, IReadOnlyList<AddonItemCommand> Items);

// Coordination sheet (BEO) and department sheets.
public sealed record SaveBeoRequest(string? Notes = null);

// Iteration 4 (FE-11): Post-event Ratings & Complaints (C-12..C-16, M-18..M-20)
public sealed record SubmitRatingsRequest(string? ProjectCode, IReadOnlyList<Domain.Rules.RatingInput> Ratings);
public sealed record CreateComplaintRequest(string? ProjectCode, int? BeoId, string Description);
public sealed record ManagerComplaintActionRequest(string Action, string Note);

// longtb: Manager task completion with proof photos.
public sealed record AccountRequest(string FullName, string Email, string? Phone, string Role);
public sealed record AccountStatusRequest(bool Active);
public sealed record AccountCredentialResponse(UserAccountView Account, string? TemporaryPassword, bool EmailQueued = true);
