namespace VanAnhPerfume.Models.Entities;

public class OrderStatusLog
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? OldPaymentStatus { get; set; }
    public string? NewPaymentStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangedBy { get; set; }
}
