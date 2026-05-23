namespace VanAnhPerfume.Models.ViewModels
{
    public class ProductDetailVM
    {

        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        /// <summary>Mô tả ngắn (plain); dùng ở intro, meta — không dùng HTML dài.</summary>
        public string? ShortDescription { get; set; }
        /// <summary>Bảng thông tin chi tiết (nhãn / giá trị).</summary>
        public List<ProductSpecRowVM> DetailSpecs { get; set; } = new();

        /// <summary>Chuỗi thuần cho meta description (không HTML).</summary>
        public string MetaDescriptionPlain { get; set; } = string.Empty;

        /// <summary>Đoạn giới thiệu dưới giá: ShortDescription hoặc đã bỏ thẻ từ Description.</summary>
        public string IntroPlain { get; set; } = string.Empty;

        public string? MainImage { get; set; }
        public string? Gender { get; set; }
        public string? Concentration { get; set; } // Nồng độ (EDP, EDT...)

        // Danh sách ảnh phụ
        public List<string> OtherImages { get; set; } = new();

        // Danh sách biến thể (Size & Giá)
        public List<VariantVM> Variants { get; set; } = new();

        // Danh sách tầng hương
        public List<NoteVM> TopNotes { get; set; } = new();
        public List<NoteVM> HeartNotes { get; set; } = new();
        public List<NoteVM> BaseNotes { get; set; } = new();
        public List<ReviewVM> Reviews { get; set; } = new();
        public double AverageRating => Reviews.Count == 0 ? 0 : Reviews.Average(r => r.Rating);
        public int ReviewCount => Reviews.Count;

        /// <summary>Cùng danh mục (đã lọc active), không gồm sản phẩm hiện tại.</summary>
        public List<ProductHomeVM> RelatedProducts { get; set; } = [];

        public bool CanReview { get; set; }
        public bool HasReviewed { get; set; }
        public ReviewVM? MyReview { get; set; }
    }

    public class ProductSpecRowVM
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class VariantVM
    {
        public int VariantId { get; set; }
        public string Size { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string FormattedPrice => Price.ToString("N0") + " ₫";
    }

    public class NoteVM
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ReviewVM
    {
        public int ReviewId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? ReviewerEmail { get; set; }
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Comment { get; set; }
        public bool IsVerifiedPurchase { get; set; }
        public string? AdminReply { get; set; }
        public DateTime? AdminReplyAt { get; set; }
        public int HelpfulCount { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}