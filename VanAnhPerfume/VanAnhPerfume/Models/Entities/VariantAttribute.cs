namespace VanAnhPerfume.Models.Entities;

public class VariantAttribute
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Product Product { get; set; } = null!;
    public ICollection<VariantAttributeValue> Values { get; set; } = new List<VariantAttributeValue>();
}
