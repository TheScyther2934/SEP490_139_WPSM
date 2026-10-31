using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class BeodepartmentSheet
{
    public int SheetId { get; set; }

    public int Beoid { get; set; }

    public string Department { get; set; } = null!;

    public string PdfFilePath { get; set; } = null!;

    public DateTime GeneratedAt { get; set; }

    public virtual Beo Beo { get; set; } = null!;
}
