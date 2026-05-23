using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.ViewModels;

public class ProfileVM
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;
}

public class MyOrderItemVM
{
    public int OrderId { get; set; }
    public DateTime? OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }
}

public class ChangePasswordVM
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Một trang Profile với tab: tránh tải lại khi chuyển tab (client-side).</summary>
public class AccountDashboardVM
{
    public ProfileVM Profile { get; set; } = new();
    public List<MyOrderItemVM> Orders { get; set; } = [];
    public ChangePasswordVM ChangePassword { get; set; } = new();
    public List<WishlistItemVM> Wishlist { get; set; } = [];
}

public class OrderTrackingVM
{
    public int OrderId { get; set; }
    public DateTime? OrderDate { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderTrackingLineVM> Lines { get; set; } = [];
    public List<OrderTrackingStatusLogVM> Timeline { get; set; } = [];
}

public class OrderTrackingLineVM
{
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderTrackingStatusLogVM
{
    public string? NewStatus { get; set; }
    public string? Note { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
