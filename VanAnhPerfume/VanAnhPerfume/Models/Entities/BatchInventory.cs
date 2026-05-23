using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class BatchInventory
{
    public int BatchId { get; set; }

    public int VariantId { get; set; }

    public string BatchNumber { get; set; } = null!;

    public DateOnly ExpiryDate { get; set; }

    public int Quantity { get; set; }

    public virtual ProductVariant Variant { get; set; } = null!;
}
