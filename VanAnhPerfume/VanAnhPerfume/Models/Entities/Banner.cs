namespace VanAnhPerfume.Models.Entities;

public class Banner
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SubTitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? MobileImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public string? Description { get; set; }
    public string? ButtonText { get; set; }
    public string? TextColor { get; set; }
    public string? Position { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DisplayFrom { get; set; }
    public DateTime? DisplayTo { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public DateTime? PublishAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
