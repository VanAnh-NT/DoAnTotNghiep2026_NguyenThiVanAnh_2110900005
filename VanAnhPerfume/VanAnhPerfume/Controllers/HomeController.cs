using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models;
using VanAnhPerfume.Models.ViewModels;
using VanAnhPerfume.Repositories; 

namespace VanAnhPerfume.Controllers
{
    public class HomeController(IProductService productService, VanAnhPerfumeContext context, Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var products = await productService.GetProductsForHomeAsync();

            var orderedBrands = await context.Brands
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder <= 0 ? int.MaxValue : x.SortOrder)
                .ThenBy(x => x.Name)
                .Take(12)
                .ToListAsync();
            ViewBag.Brands = orderedBrands;

            var homeBrands = orderedBrands;

            ViewBag.HomeBrands = homeBrands;

            var showcaseBrands = homeBrands.Take(6).ToList();
            var brandShowcases = new List<HomeBrandShowcaseVM>();

            foreach (var brand in showcaseBrands)
            {
                var showcaseProducts = await context.Products
                    .AsNoTracking()
                    .Include(p => p.Brand)
                    .Include(p => p.ProductVariants)
                    .Include(p => p.ProductImages)
                    .Where(p =>
                        p.Status == true
                        && p.ProductStatus == "Active"
                        && p.BrandId == brand.BrandId)
                    .OrderByDescending(p => p.ProductId)
                    .Take(6)
                    .Select(p => new ProductHomeVM
                    {
                        ProductId = p.ProductId,
                        ProductName = p.Name,
                        BrandName = p.Brand.Name,
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

                if (showcaseProducts.Count == 0)
                {
                    continue;
                }

                var defaultBanner = showcaseProducts.First().ImageUrl ?? "/images/hero-placeholder.svg";

                brandShowcases.Add(new HomeBrandShowcaseVM
                {
                    BrandId = brand.BrandId,
                    BrandName = brand.Name,
                    BrandSlug = brand.Slug
                        ?? brand.Name.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal),
                    BannerImageUrl = !string.IsNullOrWhiteSpace(brand.BannerUrl)
                        ? brand.BannerUrl!
                        : !string.IsNullOrWhiteSpace(brand.LogoUrl ?? brand.Logo)
                            ? (brand.LogoUrl ?? brand.Logo)!
                            : defaultBanner,
                    Products = showcaseProducts
                });
            }

            ViewBag.BrandShowcases = brandShowcases;

            ViewBag.LatestNews = await context.News
                .AsNoTracking()
                .Where(x => x.Status == true && x.NewsStatus == "Published")
                .OrderByDescending(x => x.IsFeatured == true)
                .ThenByDescending(x => x.PublishAt ?? x.CreatedAt)
                .ThenByDescending(x => x.NewsId)
                .Take(3)
                .ToListAsync(); 
            // Use local time here because admin inputs are typically local-time (DateTimeKind.Unspecified)
            // and comparing them to UTC can unintentionally hide banners.
            var now = DateTime.Now;
            List<Banner> heroBanners;
            if (!memoryCache.TryGetValue("home_banners_hero", out object? heroCache))
            {
                heroBanners = context.Banners
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive
                        && x.Position != null
                        && EF.Functions.Like(x.Position, "hero%")
                        && (!x.DisplayFrom.HasValue || x.DisplayFrom <= now)
                        && (!x.DisplayTo.HasValue || x.DisplayTo >= now)
                        && x.ImageUrl != null && x.ImageUrl != "")
                    .OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.Id)
                    .ToList();

                // If admin has banners but none match hero/time window, don't fall back to hard-coded image.
                if (heroBanners.Count == 0)
                {
                    heroBanners = context.Banners
                        .AsNoTracking()
                        .Where(x => x.IsActive && x.ImageUrl != null && x.ImageUrl != "")
                        .OrderBy(x => x.SortOrder)
                        .ThenByDescending(x => x.Id)
                        .Take(5)
                        .ToList();
                }
                var entry = memoryCache.CreateEntry("home_banners_hero");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Value = heroBanners;
                entry.Dispose();
            }
            else heroBanners = (heroCache as List<Banner>) ?? [];
            ViewBag.Banners = heroBanners;

            List<Banner> midBanners;
            if (!memoryCache.TryGetValue("home_banners_mid", out object? midCache))
            {
                midBanners = context.Banners
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive
                        && x.Position != null
                        && (EF.Functions.Like(x.Position, "mid-page%") || EF.Functions.Like(x.Position, "mid%"))
                        && (!x.DisplayFrom.HasValue || x.DisplayFrom <= now)
                        && (!x.DisplayTo.HasValue || x.DisplayTo >= now)
                        && x.ImageUrl != null && x.ImageUrl != "")
                    .OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.Id)
                    .Take(2)
                    .ToList();
                var entry = memoryCache.CreateEntry("home_banners_mid");
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Value = midBanners;
                entry.Dispose();
            }
            else midBanners = (midCache as List<Banner>) ?? [];
            ViewBag.MidBanners = midBanners;
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> FeaturedProducts(string gender = "all")
        {
            var query = context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
.Where(p =>
    p.Status == true
    && p.ProductStatus == "Active"
    && (p.IsFeatured == true || p.IsFeatured == null)
    && p.ProductVariants.Any(v => v.IsActive)); 

            if (!string.IsNullOrWhiteSpace(gender) && !string.Equals(gender, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Gender == gender);
            }

            var products = await query
                .OrderByDescending(p => p.ProductId)
                .Take(8)
                .Select(p => new ProductHomeVM
                {
                    ProductId = p.ProductId,
                    ProductName = p.Name,
                    BrandName = p.Brand.Name,
                    Price = p.ProductVariants.OrderBy(v => v.Price).Select(v => v.Price).FirstOrDefault(),
                    MaxPrice = p.ProductVariants.OrderByDescending(v => v.Price).Select(v => (decimal?)v.Price).FirstOrDefault(),
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

            return PartialView("_FeaturedProductsPartial", products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> Blog(string? tag, int page = 1)
        {
            var query = context.News
                .AsNoTracking()
                .Where(x => x.Status == true && x.NewsStatus == "Published");

            if (!string.IsNullOrWhiteSpace(tag))
            {
                query = query.Where(x => x.CategoryTag == tag);
            }

            const int pageSize = 9;

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var posts = await query
                .OrderByDescending(x => x.IsFeatured == true)
                .ThenByDescending(x => x.PublishAt ?? x.CreatedAt)
                .ThenByDescending(x => x.NewsId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tag = tag;

            ViewBag.Tags = await context.News
                .AsNoTracking()
                .Where(x =>
                    x.Status == true
                    && x.NewsStatus == "Published"
                    && x.CategoryTag != null
                    && x.CategoryTag != "")
                .Select(x => x.CategoryTag!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            return View(posts);
        }

        [HttpGet("Home/BlogDetail/{id:int}")]
        public async Task<IActionResult> BlogDetail(int id)
        {
            var post = await context.News.FirstOrDefaultAsync(x => x.NewsId == id && x.Status == true && x.NewsStatus == "Published");
            if (post == null) return NotFound();

            var viewKey = $"NewsView_{id}";
            if (!HttpContext.Session.GetInt32(viewKey).HasValue)
            {
                post.ViewCount += 1;
                post.UpdatedAt = DateTime.UtcNow;
                HttpContext.Session.SetInt32(viewKey, 1);
                await context.SaveChangesAsync();
            }

            var recent = await context.News
                .Where(x => x.Status == true && x.NewsId != id && x.NewsStatus == "Published")
                .OrderByDescending(x => x.IsFeatured == true)
                .ThenByDescending(x => x.PublishAt ?? x.CreatedAt)
                .ThenByDescending(x => x.NewsId)
                .Take(5)
                .ToListAsync();
            var related = await context.News
                .Where(x =>
                    x.Status == true
                    && x.NewsId != id
                    && x.NewsStatus == "Published"
                    && x.CategoryTag == post.CategoryTag)
                .OrderByDescending(x => x.IsFeatured == true)
                .ThenByDescending(x => x.PublishAt ?? x.CreatedAt)
                .ThenByDescending(x => x.NewsId)
                .Take(4)
                .ToListAsync();

            return View(new BlogDetailVM
            {
                CurrentPost = post,
                RecentPosts = recent,
                RelatedPosts = related
            });
        }

        [HttpGet("Home/Brand/{slug}")]
        public async Task<IActionResult> Brand(string slug, string? sort, string? gender, string? concentration, decimal? minPrice, decimal? maxPrice, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction("Index", "Product");
            }

            var normalizedSlug = slug.Trim().ToLowerInvariant();
            var brand = await context.Brands
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive && ((x.Slug ?? "").ToLower() == normalizedSlug || (x.Name != null && x.Name.ToLower().Replace(" ", "-") == normalizedSlug)));
            if (brand == null) return NotFound();

            var query = context.Products
                .AsNoTracking()
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .Include(p => p.ProductImages)
                .Where(p =>
                            p.Status == true
                            && p.ProductStatus == "Active"
                            && p.BrandId == brand.BrandId
                            && p.ProductVariants.Any(v => v.IsActive));

            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(x => x.Gender == gender);
            }
            if (!string.IsNullOrWhiteSpace(concentration))
            {
                query = query.Where(x => x.Concentration == concentration);
            }
            if (minPrice.HasValue)
            {
                query = query.Where(x => x.ProductVariants.Any(v => v.IsActive && v.Price >= minPrice.Value));
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(x => x.ProductVariants.Any(v => v.IsActive && v.Price <= maxPrice.Value));
            }

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.ProductVariants
    .Where(v => v.IsActive)
    .OrderBy(v => v.Price)
    .Select(v => v.Price)
    .FirstOrDefault()),

                "price_desc" => query.OrderByDescending(p => p.ProductVariants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.Price)
                    .Select(v => v.Price)
                    .FirstOrDefault()),
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            const int pageSize = 12;
            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var products = await query.Skip((page - 1) * pageSize).Take(pageSize)
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
                }).ToListAsync();

            return View(new BrandPageVM
            {
                Brand = brand,
                Listing = new ProductListVM
                {
                    BrandSlug = slug,
                    Sort = sort,
                    Gender = gender,
                    Concentration = concentration,
                    MinPrice = minPrice,
                    MaxPrice = maxPrice,
                    Page = page,
                    TotalPages = totalPages,
                    Products = products
                }
            });
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Policy()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return Json(new { success = false, message = "Email không hợp lệ." });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (!new EmailAddressAttribute().IsValid(email))
            {
                return Json(new { success = false, message = "Email không đúng định dạng." });
            }

            var existed = await context.NewsletterSubscribers.AnyAsync(x => x.Email == email);
            if (existed)
            {
                return Json(new { success = false, message = "Email đã đăng ký" });
            }

            context.NewsletterSubscribers.Add(new NewsletterSubscriber
            {
                Email = email,
                SubscribedAt = DateTime.UtcNow,
                IsActive = true
            });
            await context.SaveChangesAsync();
            return Json(new { success = true, message = "Đăng ký thành công!" });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("Home/Error/{statusCode:int}")]
        public IActionResult StatusCodeError(int statusCode)
        {
            ViewBag.StatusCode = statusCode;
            return View("StatusCodeError");
        }
    }

    public class SubscribeRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}

