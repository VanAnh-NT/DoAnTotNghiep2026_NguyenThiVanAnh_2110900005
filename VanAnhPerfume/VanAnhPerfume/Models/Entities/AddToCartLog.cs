namespace VanAnhPerfume.Models.Entities;

public class AddToCartLog
{
    public int Id { get; set; }
    public int? ProductId { get; set; }
    public int? VariantId { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
