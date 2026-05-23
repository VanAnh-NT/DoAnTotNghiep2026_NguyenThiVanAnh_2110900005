using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Brand
{
    public int BrandId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? Website { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? Country { get; set; }
    public string? Logo { get; set; }
    public string? Slug { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
