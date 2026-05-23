using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.ViewModels;
using VanAnhPerfume.Repositories;

namespace VanAnhPerfume.Services
{
    public class ProductService(VanAnhPerfumeContext context) : IProductService
    {
        private static readonly JsonSerializerOptions SpecJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static string StripHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var noTags = Regex.Replace(html, "<.*?>", string.Empty, RegexOptions.Singleline);
            return WebUtility.HtmlDecode(noTags).Replace('\n', ' ').Trim();
        }

        private static List<ProductSpecRowVM> ParseDetailSpecs(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                var rows = JsonSerializer.Deserialize<List<ProductSpecRowVM>>(json, SpecJsonOptions);

                if (rows == null)
                {
                    return [];
                }

                return [.. rows.Where(r =>
                    !(string.IsNullOrWhiteSpace(r.Label) &&
                      string.IsNullOrWhiteSpace(r.Value)))];
            }
            catch
            {
                return [];
            }
        }

        private static string BuildMetaPlain(string? shortDesc, string? longHtml, string name)
        {
            if (!string.IsNullOrWhiteSpace(shortDesc))
            {
                var s = shortDesc.Trim();
                return s.Length > 160 ? s[..160] : s;
            }

            var stripped = StripHtml(longHtml);

            if (!string.IsNullOrWhiteSpace(stripped))
            {
                return stripped.Length > 160 ? stripped[..160] : stripped;
            }

            return name;
        }

        private static string BuildIntroPlain(string? shortDesc, string? longHtml)
        {
            if (!string.IsNullOrWhiteSpace(shortDesc))
            {
                return shortDesc.Trim();
            }

            var stripped = StripHtml(longHtml);

            if (!string.IsNullOrWhiteSpace(stripped))
            {
                return stripped;
            }

            return "Mùi hương cân bằng giữa độ lưu hương và độ tỏa hương, phù hợp phong cách cao cấp hiện đại.";
        }

        // --- LẤY DANH SÁCH TRANG CHỦ ---
        public async Task<IEnumerable<ProductHomeVM>> GetProductsForHomeAsync()
        {
            return await context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .Where(p =>
                    p.Status == true &&
                    p.ProductVariants.Any(v => v.IsActive))
                .OrderByDescending(p => p.ProductId)
                .Select(p => new ProductHomeVM
                {
                    ProductId = p.ProductId,
                    ProductName = p.Name,
                    BrandName = p.Brand != null ? p.Brand.Name : "N/A",

                    Price = p.ProductVariants
                        .Where(v => v.IsActive)
                        .OrderBy(v => v.Price)
                        .Select(v => v.Price)
                        .FirstOrDefault(),

                    MaxPrice = p.ProductVariants
                        .Where(v => v.IsActive)
                        .OrderByDescending(v => v.Price)
                        .Select(v => (decimal?)v.Price)
                        .FirstOrDefault(),

                    ImageUrl = p.MainImage
                        ?? p.ProductImages
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.SortOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault()
                        ?? "/images/default-perfume.jpg",

                    HoverImageUrl = p.ProductImages
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .Skip(1)
                        .FirstOrDefault()
                        ?? p.ProductImages
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.SortOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault()
                        ?? p.MainImage
                        ?? "/images/default-perfume.jpg",

                    CategoryName = p.Category != null ? p.Category.Name : "N/A",
                    IsNew = p.IsFeatured ?? false
                })
                .ToListAsync();
        }

        // --- LẤY CHI TIẾT SẢN PHẨM ---
        public async Task<ProductDetailVM?> GetProductDetailAsync(int id)
        {
            var product = await context.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .Include(p => p.Notes)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                return null;
            }

            var approvedReviews = product.Reviews
                .Where(r => r.Status == "Approved")
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewVM
                {
                    ReviewId = r.ReviewId,
                    UserName = r.ReviewerName ?? (r.User != null ? r.User.FullName : "Khách"),
                    ReviewerEmail = r.ReviewerEmail,
                    Rating = r.Rating,
                    Title = r.Title,
                    Content = r.Content ?? r.Comment,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    IsVerifiedPurchase = r.IsVerifiedPurchase,
                    AdminReply = r.AdminReply,
                    AdminReplyAt = r.AdminReplyAt,
                    HelpfulCount = r.HelpfulCount
                })
                .ToList();

            var relatedProducts = await context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .Where(p =>
                    p.Status == true &&
                    p.ProductStatus == "Active" &&
                    p.Brand.IsActive &&
                    p.Category.IsActive &&
                    p.CategoryId == product.CategoryId &&
                    p.ProductId != product.ProductId &&
                    p.ProductVariants.Any(v => v.IsActive))
                .OrderByDescending(p => p.ProductId)
                .Take(4)
                .Select(p => new ProductHomeVM
                {
                    ProductId = p.ProductId,
                    ProductName = p.Name,
                    BrandName = p.Brand!.Name,
                    CategoryName = p.Category!.Name,

                    Price = p.ProductVariants
                        .Where(v => v.IsActive)
                        .OrderBy(v => v.Price)
                        .Select(v => v.Price)
                        .FirstOrDefault(),

                    MaxPrice = p.ProductVariants
                        .Where(v => v.IsActive)
                        .OrderByDescending(v => v.Price)
                        .Select(v => (decimal?)v.Price)
                        .FirstOrDefault(),

                    ImageUrl = p.MainImage
                        ?? p.ProductImages
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.SortOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault()
                        ?? "/images/default-perfume.jpg",

                    HoverImageUrl = p.ProductImages
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .Skip(1)
                        .FirstOrDefault()
                        ?? p.ProductImages
                            .OrderByDescending(i => i.IsPrimary)
                            .ThenBy(i => i.SortOrder)
                            .Select(i => i.ImageUrl)
                            .FirstOrDefault()
                        ?? p.MainImage
                        ?? "/images/default-perfume.jpg",

                    IsNew = p.IsFeatured ?? false
                })
                .ToListAsync();

            return new ProductDetailVM
            {
                ProductId = product.ProductId,
                Name = product.Name,
                BrandName = product.Brand?.Name ?? "N/A",
                CategoryName = product.Category?.Name ?? "N/A",
                Description = product.Description,
                ShortDescription = product.ShortDescription,
                DetailSpecs = ParseDetailSpecs(product.DetailSpecsJson),
                MetaDescriptionPlain = BuildMetaPlain(product.ShortDescription, product.Description, product.Name),
                IntroPlain = BuildIntroPlain(product.ShortDescription, product.Description),
                MainImage = product.MainImage ?? "/images/default-perfume.jpg",
                Gender = product.Gender,
                Concentration = product.Concentration,

                OtherImages = product.ProductImages
                    .OrderByDescending(img => img.IsPrimary)
                    .ThenBy(img => img.SortOrder)
                    .Select(img => img.ImageUrl)
                    .ToList(),

                // Chỉ hiển thị biến thể đang kích hoạt.
                // Biến thể cũ đã xóa mềm hoặc bỏ kích hoạt sẽ không hiện ở trang chi tiết.
                Variants = product.ProductVariants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.VariantId)
                    .Select(v => new VariantVM
                    {
                        VariantId = v.VariantId,
                        Size = v.Size,
                        Price = v.Price
                    })
                    .ToList(),

                TopNotes = product.Notes
                    .Where(n => n.Type == "Top")
                    .Select(n => new NoteVM { Name = n.Name })
                    .ToList(),

                HeartNotes = product.Notes
                    .Where(n => n.Type == "Heart")
                    .Select(n => new NoteVM { Name = n.Name })
                    .ToList(),

                BaseNotes = product.Notes
                    .Where(n => n.Type == "Base")
                    .Select(n => new NoteVM { Name = n.Name })
                    .ToList(),

                Reviews = approvedReviews,
                RelatedProducts = relatedProducts
            };
        }
    }
}