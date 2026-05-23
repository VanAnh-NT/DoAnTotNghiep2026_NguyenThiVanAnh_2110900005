namespace VanAnhPerfume.Models.Entities;

public class ProductVariantAttributeValue
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int VariantAttributeValueId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
    public VariantAttributeValue VariantAttributeValue { get; set; } = null!;
}
