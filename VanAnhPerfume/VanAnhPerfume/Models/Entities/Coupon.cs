using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.Entities;

public class Coupon
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string DiscountType { get; set; } = "Percent";

    public decimal Value { get; set; }
    public decimal MinOrderValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public int? PerUserLimit { get; set; }
    public DateTime? StartDate { get; set; }
    public string? ApplicableCategoryIds { get; set; }
    public string? ApplicableProductIds { get; set; }
    public bool AutoApply { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
