using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.ViewModels
{
    public class ProductHomeVM
    {
        public int ProductId { get; set; }

        [Display(Name = "Tên sản phẩm")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Thương hiệu")]
        public string BrandName { get; set; } = "VanAnh Perfume";

        public decimal Price { get; set; }
        public decimal? MaxPrice { get; set; }

        // --- CÁC THÀNH PHẦN NÂNG CAO ---

        public decimal? DiscountPrice { get; set; } // Giá sau khi giảm (nếu có)

        public string? ImageUrl { get; set; }
        public string? HoverImageUrl { get; set; }

        public string? CategoryName { get; set; }

        public bool IsNew { get; set; } // Để hiển thị nhãn "NEW" góc ảnh

        public int StockQuantity { get; set; } // Để hiện nhãn "HẾT HÀNG" nếu số lượng = 0

        // --- PHƯƠNG THỨC HỖ TRỢ GIAO DIỆN ---

        public bool IsOnSale => DiscountPrice.HasValue && DiscountPrice < Price;

        public string FormattedPrice
        {
            get
            {
                if (MaxPrice.HasValue && MaxPrice.Value > Price)
                {
                    return $"{Price:N0} ₫ - {MaxPrice.Value:N0} ₫";
                }

                return Price.ToString("N0") + " ₫";
            }
        }

        public string FormattedDiscountPrice => DiscountPrice?.ToString("N0") + " ₫";
    }
}