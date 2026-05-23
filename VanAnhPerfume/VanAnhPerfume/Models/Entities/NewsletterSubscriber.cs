using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.Entities;

public class NewsletterSubscriber
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
