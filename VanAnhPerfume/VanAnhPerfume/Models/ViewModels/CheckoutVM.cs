using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.ViewModels
{
    public class CheckoutVM
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên của bạn")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(0|\+84)[0-9]{9,10}$", ErrorMessage = "Số điện thoại không đúng định dạng (VD: 0987654321 hoặc +84987654321)")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng cung cấp địa chỉ nhận hàng")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; } = string.Empty;
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string? Email { get; set; }
        public string? ShippingProvince { get; set; }
        public string? ShippingDistrict { get; set; }
        public string? Note { get; set; }

        // Danh sách sản phẩm trong giỏ
        public List<CartItemVM> CartItems { get; set; } = new();

        public string? PromotionCode { get; set; }
        public int? PromotionId { get; set; }
        public decimal DiscountAmount { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD";

        // Tự động tính tổng tiền từ danh sách CartItems
        // Dùng công thức này để tránh lỗi "Read-only" trong Controller
        public decimal TotalAmount => CartItems != null ? CartItems.Sum(i => i.Total) : 0;
        public decimal FinalAmount => Math.Max(0, TotalAmount - DiscountAmount);
    }
}