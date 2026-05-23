using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class ProductVariant
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    public string Size { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }

    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Sku { get; set; } = null!;
    public string? Color { get; set; }
    public string? Material { get; set; }
    public string? VariantImageUrl { get; set; }

    public virtual ICollection<BatchInventory> BatchInventories { get; set; } = new List<BatchInventory>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public virtual ICollection<ProductVariantAttributeValue> ProductVariantAttributeValues { get; set; } = new List<ProductVariantAttributeValue>();
    public virtual Inventory? Inventory { get; set; }

    public virtual Product Product { get; set; } = null!;
}
