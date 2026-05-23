using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Category
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;
    public int? ParentId { get; set; }
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
