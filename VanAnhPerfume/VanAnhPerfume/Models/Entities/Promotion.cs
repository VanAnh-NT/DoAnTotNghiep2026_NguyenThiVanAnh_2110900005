using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Promotion
{
    public int PromoId { get; set; }

    public string Name { get; set; } = null!;

    public int DiscountPercent { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool? IsActive { get; set; }
    public decimal? MinOrderValue { get; set; }
    public string? ApplicableCategoryIds { get; set; }
    public string? ApplicableProductIds { get; set; }
    public bool AutoApply { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
