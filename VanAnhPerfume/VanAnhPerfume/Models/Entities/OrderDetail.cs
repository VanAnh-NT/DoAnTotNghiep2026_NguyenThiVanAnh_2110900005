using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class OrderDetail
{
    public int OrderDetailId { get; set; }

    public int OrderId { get; set; }

    public int VariantId { get; set; }
    public string? ProductName { get; set; }
    public string? VariantName { get; set; }
    public string? Sku { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
