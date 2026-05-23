namespace VanAnhPerfume.Models.ViewModels;

public class AdminInventoryRowVM
{
    public int VariantId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantitySold { get; set; }
    public int LowStockThreshold { get; set; }
    public int Sellable => Math.Max(0, QuantityAvailable - QuantityReserved);
    public bool IsOutOfStock => Sellable == 0;
    public bool IsLowStock => Sellable > 0 && Sellable <= LowStockThreshold;
}

public class BulkImportPreviewRowVM
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public int? VariantId { get; set; }
}
