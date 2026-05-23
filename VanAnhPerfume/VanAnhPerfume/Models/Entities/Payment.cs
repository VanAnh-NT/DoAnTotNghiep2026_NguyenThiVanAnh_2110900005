using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string? PaymentMethod { get; set; }

    public decimal Amount { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentGatewayResponse { get; set; }
    public DateTime? RefundedAt { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundNote { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<PaymentLog> PaymentLogs { get; set; } = new List<PaymentLog>();
}
