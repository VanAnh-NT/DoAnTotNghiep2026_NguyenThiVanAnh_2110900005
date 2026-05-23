using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels;
using VanAnhPerfume.Repositories;

namespace VanAnhPerfume.Controllers
{
    public class ProductController(IProductService productService, VanAnhPerfumeContext context) : Controller
    {
        public async Task<IActionResult> Index(string? q, string? brand, string? category, string? sort, string? gender, string? concentration, decimal? minPrice, decimal? maxPrice, int page = 1)
        {
            var (model, selectedConcentrations) = await BuildProductListModelAsync(
                q, brand, category, sort, gender, concentration, minPrice, maxPrice, page, Request.Query);

            ViewBag.Gender = gender;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Concentrations = selectedConcentrations;

            if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return PartialView("_ProductListingMain", model);

            return View(model);
        }

        private async Task<(ProductListVM Model, List<string> SelectedConcentrations)> BuildProductListModelAsync(
            string? q,
            string? brand,
            string? category,
            string? sort,
            string? gender,
            string? concentration,
            decimal? minPrice,
            decimal? maxPrice,
            int page,
            IQueryCollection queryCollection)
        {
            var query = context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .Where(p => p.Status == true && p.ProductStatus == "Active" && p.Brand.IsActive && p.Category.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => p.Name.Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var b = brand.Trim().ToLowerInvariant();
                query = query.Where(p => (p.Brand.Slug ?? "").ToLower() == b);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var categorySlug = category.Trim().ToLowerInvariant();
                var selectedCategory = await context.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Slug != null && x.Slug.ToLower() == categorySlug && x.IsActive);
                if (selectedCategory != null)
                {
                    var categoryIds = await context.Categories.AsNoTracking()
                        .Where(x => x.CategoryId == selectedCategory.CategoryId || x.ParentId == selectedCategory.CategoryId)
                        .Select(x => x.CategoryId)
                        .ToListAsync();
                    query = query.Where(p => categoryIds.Contains(p.CategoryId));
                }
            }
            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(p => p.Gender == gender);
            }

            var selectedConcentrations = queryCollection["concentration"]
                .Select(x => x?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct()
                .ToList();
            if (selectedConcentrations.Count == 0 && !string.IsNullOrWhiteSpace(concentration))
            {
                selectedConcentrations = concentration
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct()
                    .ToList();
            }

            if (selectedConcentrations.Count > 0)
            {
                query = query.Where(p => selectedConcentrations.Contains(p.Concentration!));
            }
            if (minPrice.HasValue)
            {
                query = query.Where(x => x.ProductVariants.Any(v => v.Price >= minPrice.Value));
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(x => x.ProductVariants.Any(v => v.Price <= maxPrice.Value));
            }

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.ProductVariants.OrderBy(v => v.Price).Select(v => v.Price).FirstOrDefault()),
                "price_desc" => query.OrderByDescending(p => p.ProductVariants.OrderBy(v => v.Price).Select(v => v.Price).FirstOrDefault()),
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            const int pageSize = 12;
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductHomeVM
                {
                    ProductId = p.ProductId,
                    ProductName = p.Name,
                    BrandName = p.Brand.Name,
                    CategoryName = p.Category.Name,
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
                        ?? p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault()
                        ?? "/images/default-perfume.jpg",
                    HoverImageUrl = p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).Skip(1).FirstOrDefault()
                        ?? p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.SortOrder).Select(i => i.ImageUrl).FirstOrDefault()
                        ?? p.MainImage
                        ?? "/images/default-perfume.jpg",
                    IsNew = p.IsFeatured ?? false
                })
                .ToListAsync();

            var model = new ProductListVM
            {
                Query = q,
                Brand = brand,
                Category = category,
                Sort = sort,
                Gender = gender,
                Concentration = selectedConcentrations.FirstOrDefault(),
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Page = page,
                TotalPages = totalPages,
                TotalItems = totalItems,
                PageSize = pageSize,
                Products = products,
                Brands = await context.Brands.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                    .Select(x => new BrandFilterItemVM { Name = x.Name, Slug = x.Slug ?? x.Name.ToLower().Replace(" ", "-") }).ToListAsync(),
                Categories = await context.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                    .Select(x => new CategoryFilterItemVM { Id = x.CategoryId, Name = x.Name, Slug = x.Slug ?? x.Name.ToLower().Replace(" ", "-"), ParentId = x.ParentId }).ToListAsync()
            };

            return (model, selectedConcentrations);
        }

        [HttpGet]
        public async Task<IActionResult> SearchSuggest(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            {
                return Json(Array.Empty<SearchSuggestVM>());
            }

            var query = q.Trim();
            var data = await context.Products
                .AsNoTracking()
                .Include(x => x.ProductVariants)
                .Where(x => x.Status == true && x.Name.Contains(query))
                .OrderBy(x => x.Name)
                .Take(6)
                .Select(x => new SearchSuggestVM
                {
                    Id = x.ProductId,
                    Name = x.Name,
                    PrimaryImageUrl = x.MainImage ?? "/images/default-perfume.jpg",
                    Price = x.ProductVariants.OrderBy(v => v.Price).Select(v => v.Price).FirstOrDefault()
                })
                .ToListAsync();

            return Json(data);
        }

        public async Task<IActionResult> Detail(int id)
        {
            var product = await productService.GetProductDetailAsync(id);
            if (product == null) return NotFound();
            var userIdClaim = User.FindFirstValue("UserId");
            if (int.TryParse(userIdClaim, out var userId))
            {
                product.HasReviewed = await context.Reviews.AnyAsync(x => x.ProductId == id && x.UserId == userId);
                product.MyReview = await context.Reviews
                    .Where(x => x.ProductId == id && x.UserId == userId)
                    .Select(x => new ReviewVM
                    {
                        ReviewId = x.ReviewId,
                        UserName = x.ReviewerName ?? (x.User != null ? x.User.FullName : "Bạn"),
                        ReviewerEmail = x.ReviewerEmail,
                        Rating = x.Rating,
                        Title = x.Title,
                        Content = x.Content ?? x.Comment,
                        Comment = x.Comment,
                        CreatedAt = x.CreatedAt,
                        IsVerifiedPurchase = x.IsVerifiedPurchase,
                        AdminReply = x.AdminReply,
                        AdminReplyAt = x.AdminReplyAt,
                        HelpfulCount = x.HelpfulCount
                    }).FirstOrDefaultAsync();
                product.CanReview = true;
            }

            return View(product);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddReview(int id, CreateReviewVM model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Detail), new { id });
            }

            var userIdClaim = User.FindFirstValue("UserId");
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Detail), new { id }) });
            }

            var purchased = await context.OrderDetails
                .Include(x => x.Order)
                .Include(x => x.Variant)
                .AnyAsync(x => x.Order.UserId == userId && x.Variant.ProductId == id && x.Order.Status == "Completed");

            var alreadyReviewed = await context.Reviews.AnyAsync(x => x.ProductId == id && x.UserId == userId);
            if (alreadyReviewed)
            {
                TempData["Error"] = "Bạn đã đánh giá sản phẩm này.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            context.Reviews.Add(new Review
            {
                ProductId = id,
                UserId = userId,
                ReviewerName = User.Identity?.Name,
                ReviewerEmail = User.FindFirstValue(ClaimTypes.Email),
                Rating = model.Rating,
                Title = model.Title,
                Content = model.Content,
                Comment = model.Content,
                Status = "Pending",
                IsVerifiedPurchase = purchased,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            TempData["Success"] = "Cảm ơn! Đánh giá đang chờ duyệt.";

            return RedirectToAction(nameof(Detail), new { id });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditReview(int id, CreateReviewVM model)
        {
            var userIdClaim = User.FindFirstValue("UserId");
            if (!int.TryParse(userIdClaim, out var userId)) return RedirectToAction(nameof(Detail), new { id });
            var review = await context.Reviews.FirstOrDefaultAsync(x => x.ProductId == id && x.UserId == userId);
            if (review == null) return RedirectToAction(nameof(Detail), new { id });
            review.Rating = model.Rating;
            review.Title = model.Title;
            review.Content = model.Content;
            review.Comment = model.Content;
            review.Status = "Pending";
            review.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật đánh giá, đang chờ duyệt lại.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpPost("Product/ReviewHelpful/{id:int}")]
        public async Task<IActionResult> ReviewHelpful(int id)
        {
            var key = $"ReviewHelpful_{id}";
            if (HttpContext.Session.GetInt32(key).HasValue) return Json(new { success = false, message = "Bạn đã đánh dấu hữu ích trước đó." });
            var review = await context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id && x.Status == "Approved");
            if (review == null) return Json(new { success = false });
            review.HelpfulCount += 1;
            review.UpdatedAt = DateTime.UtcNow;
            HttpContext.Session.SetInt32(key, 1);
            await context.SaveChangesAsync();
            return Json(new { success = true, helpfulCount = review.HelpfulCount });
        }
    }
}