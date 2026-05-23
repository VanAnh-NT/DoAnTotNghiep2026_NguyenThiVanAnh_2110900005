using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Transaction
{
    public int TransactionId { get; set; }

    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public string TransactionType { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
