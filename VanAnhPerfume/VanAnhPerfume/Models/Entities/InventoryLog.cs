namespace VanAnhPerfume.Models.Entities;

public class InventoryLog
{
    public int Id { get; set; }
    public int VariantId { get; set; }
    public string ActionType { get; set; } = "Adjust";
    public string ChangeType { get; set; } = "Adjustment";
    public int QuantityDelta { get; set; }
    public int QuantityChange { get; set; }
    public int StockAfter { get; set; }
    public int QuantityAfter { get; set; }
    public string? Note { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
