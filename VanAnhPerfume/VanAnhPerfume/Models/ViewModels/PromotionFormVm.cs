using System.ComponentModel.DataAnnotations;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Models.ViewModels;

/// <summary>Form tạo/sửa chương trình khuyến mãi — validation hiển thị trên Create.cshtml.</summary>
public class PromotionFormVm : IValidatableObject
{
    public int PromoId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên chương trình khuyến mãi.")]
    [StringLength(200, ErrorMessage = "Tên chương trình khuyến mãi tối đa 200 ký tự.")]
    [Display(Name = "Tên chương trình khuyến mãi")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 90, ErrorMessage = "Phần trăm giảm phải từ 1 đến 90.")]
    [Display(Name = "Phần trăm giảm (%)")]
    public int DiscountPercent { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
    [Display(Name = "Ngày kết thúc")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Giá trị đơn hàng tối thiểu")]
    public decimal? MinOrderValue { get; set; }

    [Display(Name = "Danh mục áp dụng")]
    public string? ApplicableCategoryIds { get; set; }

    [Display(Name = "Sản phẩm áp dụng")]
    public string? ApplicableProductIds { get; set; }

    [Display(Name = "Đang kích hoạt")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Tự động áp dụng")]
    public bool AutoApply { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinOrderValue.HasValue && MinOrderValue.Value < 0)
        {
            yield return new ValidationResult(
                "Giá trị đơn hàng tối thiểu không được âm.",
                new[] { nameof(MinOrderValue) });
        }

        if (StartDate.HasValue && EndDate.HasValue)
        {
            if (EndDate.Value.Date < StartDate.Value.Date)
            {
                yield return new ValidationResult(
                    "Ngày kết thúc phải cùng ngày hoặc sau ngày bắt đầu.",
                    new[] { nameof(EndDate), nameof(StartDate) });
            }
        }
    }

    public static PromotionFormVm FromEntity(Promotion p) => new()
    {
        PromoId = p.PromoId,
        Name = p.Name,
        DiscountPercent = p.DiscountPercent,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        MinOrderValue = p.MinOrderValue,
        ApplicableCategoryIds = p.ApplicableCategoryIds,
        ApplicableProductIds = p.ApplicableProductIds,
        IsActive = p.IsActive == true,
        AutoApply = p.AutoApply
    };

    public Promotion ToEntity()
    {
        var start = StartDate!.Value.Date;
        var end = EndDate!.Value.Date;

        return new Promotion
        {
            PromoId = PromoId,
            Name = Name.Trim(),
            DiscountPercent = DiscountPercent,
            StartDate = start,
            EndDate = end,
            MinOrderValue = MinOrderValue,
            ApplicableCategoryIds = string.IsNullOrWhiteSpace(ApplicableCategoryIds)
                ? null
                : ApplicableCategoryIds.Trim(),
            ApplicableProductIds = string.IsNullOrWhiteSpace(ApplicableProductIds)
                ? null
                : ApplicableProductIds.Trim(),
            IsActive = IsActive,
            AutoApply = AutoApply
        };
    }
}