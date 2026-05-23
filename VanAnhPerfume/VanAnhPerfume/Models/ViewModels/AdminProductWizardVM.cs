using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.ViewModels;

public class AdminProductWizardVM
{
    public int? ProductId { get; set; }

    [Required]
    [StringLength(150, ErrorMessage = "Tên sản phẩm tối đa 150 ký tự (theo cấu hình lưu trữ).")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public string Gender { get; set; } = "Unisex";
    public string Concentration { get; set; } = "EDP";
    [StringLength(1000, ErrorMessage = "Mô tả ngắn tối đa 1000 ký tự.")]
    public string? ShortDescription { get; set; }

    /// <summary>Mô tả HTML (TinyMCE). Cột DB nvarchar(max) — không giới hạn ký tự cố định trong schema.</summary>
    public string? Description { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;

    public string AttributesJson { get; set; } = "[]";
    public string VariantsJson { get; set; } = "[]";
    public string ExistingImagesJson { get; set; } = "[]";

    /// <summary>Mảng JSON [{ "label", "value" }] — bảng thông tin chi tiết trên trang sản phẩm.</summary>
    public string DetailSpecsJson { get; set; } = "[]";
}

public class VariantAttributeInput
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

public class VariantRowInput
{
    public int? VariantId { get; set; }
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public int QuantityAvailable { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public List<string> Values { get; set; } = [];
}
