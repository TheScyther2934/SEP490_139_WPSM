using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int ProjectId { get; set; }

    public string PaymentType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateOnly? DueDate { get; set; }

    public decimal? Amount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? ReceiptFilePath { get; set; }

    public int? RecordedBy { get; set; }

    public DateOnly? PaidDate { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual User? RecordedByNavigation { get; set; }
}
