namespace VanAnhPerfume.Models.Entities;

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsUsed { get; set; }

    public virtual User User { get; set; } = null!;
}
