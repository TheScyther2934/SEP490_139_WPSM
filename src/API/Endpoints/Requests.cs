using Application.Persistence;

namespace API.Endpoints;

// Request/response bodies of the WPSM API. This namespace takes precedence over the older,
// differently shaped records with the same names in API (WpsmPortalStore.cs).
public sealed record CreateProjectRequest(string CoupleName, string CustomerEmail, string CustomerPhone, string Hall, string Slot,
    DateOnly EventDate, decimal BaseValue, decimal NrdAmount, string PaymentMethod = "Cash", string? ReceiptFilePath = null);
public sealed record CreatedProjectResponse(WeddingProjectView Project, string? TemporaryPassword, string EmailStatus = "ExistingAccount");
public sealed record EmailStatusRequest(string? MessageId, string? Status, string? Error);
public sealed record SpecificationRequest(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyRequest(decimal NrdRatio, decimal T30Ratio, int CrPenaltyDays, decimal CrPenaltyPercent, DateOnly? EffectiveFrom = null);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record ResolveComplaintRequest(string Decision);
public sealed record ExceptionDecisionRequest(bool Approve, string Note);

// FE-05: C-06 ticket body and the M-08 negotiation bodies.
public sealed record CrSaveRequest(string Type, string Description, IReadOnlyList<CrLineCommand>? Items, bool Submit = false);
public sealed record CrCounterRequest(IReadOnlyList<CrLineCommand>? Items, string? Note = null);
public sealed record CrApproveRequest(string? Note = null);
public sealed record CrRejectRequest(string Reason);
public sealed record CrExceptionRequest(string Note);

// FE-07: M-21 allocation dialog and the M-22 exception request.
public sealed record ResourceAllocationRequest(int ResourceId, int ProjectId, DateTime StartTime, DateTime EndTime, int Quantity,
    bool RequestException = false, string? ExceptionNote = null);
public sealed record ResourceExceptionRequest(string Note);

// Iteration 2 (FE-06): Change Requests, Cost Impact & Addendum (C-06, C-07, C-08, M-09)
public sealed record CreateCustomerCrRequest(string? ProjectCode, string Type, string Description, IReadOnlyList<CreateCrItemInput> Items);
public sealed record CustomerCrDecisionRequest(string Action, string? Note = null);
public sealed record GenerateAddendumRequest(Guid? SignedFileId = null, string? Note = null);
public sealed record AttachSignedAddendumRequest(Guid FileId);

// Iteration 3 (FE-04): BEO & Department Sheets (M-10, M-11, M-12)
public sealed record SaveBeoRequest(string? Notes = null);

// Iteration 4 (FE-11): Post-event Ratings & Complaints (C-12..C-16, M-18..M-20)
public sealed record SubmitRatingsRequest(string? ProjectCode, IReadOnlyList<ModuleRatingInput> Ratings);
public sealed record CreateComplaintRequest(string? ProjectCode, int? BeoId, string Description);
public sealed record ManagerComplaintActionRequest(string Action, string Note);
