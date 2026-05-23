namespace VanAnhPerfume.Models.Entities;

public class VariantAttributeValue
{
    public int Id { get; set; }
    public int VariantAttributeId { get; set; }
    public string Value { get; set; } = string.Empty;

    public VariantAttribute VariantAttribute { get; set; } = null!;
    public ICollection<ProductVariantAttributeValue> ProductVariantAttributeValues { get; set; } = new List<ProductVariantAttributeValue>();
}
