using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class PaymentLog
{
    public int LogId { get; set; }

    public int PaymentId { get; set; }
    public int? OrderId { get; set; }

    public DateTime? LogDate { get; set; }

    public string? RawData { get; set; }

    public string? Message { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Note { get; set; }
    public string? Source { get; set; }

    public virtual Payment Payment { get; set; } = null!;
}
