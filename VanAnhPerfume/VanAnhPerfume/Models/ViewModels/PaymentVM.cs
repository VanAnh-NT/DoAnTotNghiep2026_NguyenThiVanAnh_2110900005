namespace VanAnhPerfume.Models.ViewModels;

public class PaymentVM
{
    public int OrderId { get; set; }
    public string? OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Pending";
    public string? Message { get; set; }
    public string? ResponseCode { get; set; }
    /// <summary>Email khách dùng cho dòng thông báo trên trang thành công.</summary>
    public string? CustomerEmail { get; set; }
}
