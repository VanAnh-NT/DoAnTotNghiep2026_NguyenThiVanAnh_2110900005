using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Product
{
    public int ProductId { get; set; }

    public string Name { get; set; } = null!;

    public int BrandId { get; set; }

    public int CategoryId { get; set; }

    public string? Gender { get; set; }

    public string? Concentration { get; set; }

    public string? Description { get; set; }
    public string? ShortDescription { get; set; }

    /// <summary>JSON mảng [{ "label", "value" }] — thông tin chi tiết hiển thị dạng bảng.</summary>
    public string? DetailSpecsJson { get; set; }

    public string? MainImage { get; set; }

    public bool? IsFeatured { get; set; }

    public bool? Status { get; set; }
    public string? ProductStatus { get; set; }
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? RelatedProductIds { get; set; }
    public decimal? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Brand Brand { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();
    public virtual ICollection<VariantAttribute> VariantAttributes { get; set; } = new List<VariantAttribute>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<FragranceNote> Notes { get; set; } = new List<FragranceNote>();

    public virtual ICollection<Promotion> Promos { get; set; } = new List<Promotion>();
}
