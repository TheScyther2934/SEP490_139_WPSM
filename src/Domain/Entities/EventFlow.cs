namespace Domain.Entities;

// Entities of the event stage flow (Database/Updates/20261118_EventStageFlow.sql).

/// <summary>A note the operator writes for a stage: tasting, fitting, an adjustment (with the total before / after) or a general remark.</summary>
public partial class StageNote
{
    public int StageNoteId { get; set; }
    public int ProjectId { get; set; }
    public byte Stage { get; set; }
    public string NoteType { get; set; } = null!;
    public string Content { get; set; } = null!;
    public decimal? ValueBefore { get; set; }
    public decimal? ValueAfter { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
    public virtual User CreatedByNavigation { get; set; } = null!;
}

/// <summary>Extra services for a locked event. No deposit; counted in the settlement only once the customer confirms.</summary>
public partial class AddonContract
{
    public int AddonId { get; set; }
    public string AddonCode { get; set; } = null!;
    public int ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Note { get; set; }
    public string Status { get; set; } = "PendingCustomer";
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? RemindedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
    public virtual User CreatedByNavigation { get; set; } = null!;
    public virtual ICollection<AddonContractItem> Items { get; set; } = new List<AddonContractItem>();
}

public class AddonContractItem
{
    public int AddonItemId { get; set; }
    public int AddonId { get; set; }
    public int? CatalogItemId { get; set; }
    public string Description { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? LineTotal { get; set; }

    public virtual AddonContract Addon { get; set; } = null!;
    public virtual ServiceCatalogItem? CatalogItem { get; set; }
}

/// <summary>Evidence that a supplier was booked (transfer note, invoice or contract). No purchase-order tracking.</summary>
public partial class SupplierEvidence
{
    public int EvidenceId { get; set; }
    public int ProjectId { get; set; }
    public int? SpecItemId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string EvidenceType { get; set; } = null!;
    public Guid FileId { get; set; }
    public string? Note { get; set; }
    public int UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
    public virtual SpecItem? SpecItem { get; set; }
    public virtual StoredFile File { get; set; } = null!;
    public virtual User UploadedByNavigation { get; set; } = null!;
}

/// <summary>Final invoice of an event: adjusted contract + confirmed addon contracts - deposit, paid once.</summary>
public partial class SettlementInvoice
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public int ProjectId { get; set; }
    public decimal ContractTotal { get; set; }
    public decimal AddonTotal { get; set; }
    public decimal DepositPaid { get; set; }
    public decimal? AmountDue { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "Issued";
    public int? IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
    public virtual ICollection<SettlementInvoiceLine> Lines { get; set; } = new List<SettlementInvoiceLine>();
}

public class SettlementInvoiceLine
{
    public int LineId { get; set; }
    public int InvoiceId { get; set; }
    public string LineType { get; set; } = null!;
    public int? AddonId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }

    public virtual SettlementInvoice Invoice { get; set; } = null!;
}

/// <summary>Shared settings shown to users, e.g. the hotline.</summary>
public partial class SystemSetting
{
    public string SettingKey { get; set; } = null!;
    public string SettingValue { get; set; } = null!;
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
