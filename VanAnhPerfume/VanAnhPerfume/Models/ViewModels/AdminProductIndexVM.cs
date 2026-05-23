namespace VanAnhPerfume.Models.ViewModels;

public class AdminProductIndexVM
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? MainImage { get; set; }
    public bool IsActive { get; set; }
    public int VariantCount { get; set; }
    public int TotalAvailable { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
}

public class ProductInventoryRowVM
{
    public int VariantId { get; set; }
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantitySold { get; set; }
    public int LowStockThreshold { get; set; }
    public int Sellable => QuantityAvailable - QuantityReserved;
}
