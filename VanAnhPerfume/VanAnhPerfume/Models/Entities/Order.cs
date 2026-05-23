using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Order
{
    public int OrderId { get; set; }
    public string? OrderCode { get; set; }

    public int? UserId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Status { get; set; }

    public string FullName { get; set; } = null!;
    public string? CustomerEmail { get; set; }
    public string? ShippingProvince { get; set; }
    public string? ShippingDistrict { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public int? CouponId { get; set; }
    public string? CouponCode { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string Phone { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? PaymentStatus { get; set; }
    public string? ShippingStatus { get; set; }
    public string? TrackingCode { get; set; }
    public string? AdminNote { get; set; }
    public string? RefundStatus { get; set; }

    /// <summary>Đánh dấu đã gửi email xác nhận (COD hoặc VNPay thành công), tránh trùng IPN/Return.</summary>
    public DateTime? ConfirmationEmailSentAt { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual OrderGifting? OrderGifting { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual User User { get; set; } = null!;
}
