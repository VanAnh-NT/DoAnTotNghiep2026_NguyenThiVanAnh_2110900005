namespace VanAnhPerfume.Models.Entities;

public class Inventory
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantitySold { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ProductVariant ProductVariant { get; set; } = null!;
}
