using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BrandController(VanAnhPerfumeContext context, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index(string? q, bool? isActive, string? sort, int page = 1)
    {
        var query = context.Brands.AsNoTracking()
            .Select(x => new
            {
                Brand = x,
                ProductCount = context.Products.Count(p => p.BrandId == x.BrandId && (p.Status ?? true))
            })
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Brand.Name.Contains(q));
        if (isActive.HasValue) query = query.Where(x => x.Brand.IsActive == isActive);
        query = sort switch
        {
            "name_asc" => query.OrderBy(x => x.Brand.Name),
            "products_desc" => query.OrderByDescending(x => x.ProductCount).ThenBy(x => x.Brand.Name),
            _ => query.OrderByDescending(x => x.Brand.CreatedAt ?? DateTime.MinValue)
        };

        const int pageSize = 20;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = rows.Select(x => x.Brand).ToList();
        var productCounts = rows.ToDictionary(x => x.Brand.BrandId, x => x.ProductCount);

        ViewBag.ProductCounts = productCounts;
        ViewBag.Query = q; ViewBag.IsActive = isActive; ViewBag.Sort = sort; ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? id = null)
    {
        if (!id.HasValue) return View(new Brand { IsActive = true });
        var brand = await context.Brands.FirstOrDefaultAsync(x => x.BrandId == id.Value);
        if (brand == null) return RedirectToAction(nameof(Index));
        return View(brand);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Brand model, IFormFile? logoFile, IFormFile? bannerFile)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Slug = ToSlug(string.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug);
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["Error"] = "Tên thương hiệu là bắt buộc.";
            return RedirectToAction(nameof(Create), new { id = model.BrandId == 0 ? (int?)null : model.BrandId });
        }
        if (await context.Brands.AnyAsync(x => x.Slug == model.Slug && x.BrandId != model.BrandId))
        {
            TempData["Error"] = "Slug đã tồn tại.";
            return RedirectToAction(nameof(Create), new { id = model.BrandId == 0 ? (int?)null : model.BrandId });
        }
        if (logoFile != null && logoFile.Length > 500 * 1024)
        {
            TempData["Error"] = "Logo vượt quá 500KB.";
            return RedirectToAction(nameof(Create), new { id = model.BrandId == 0 ? (int?)null : model.BrandId });
        }

        var uploadedLogo = await SaveUploadAsync(logoFile, "brands");
        var uploadedBanner = await SaveUploadAsync(bannerFile, "brands");
        var nextLogoUrl = !string.IsNullOrWhiteSpace(uploadedLogo) ? uploadedLogo : model.LogoUrl;
        var nextBannerUrl = !string.IsNullOrWhiteSpace(uploadedBanner) ? uploadedBanner : model.BannerUrl;

        if (model.BrandId > 0)
        {
            var entity = await context.Brands.FirstOrDefaultAsync(x => x.BrandId == model.BrandId);
            if (entity == null) return RedirectToAction(nameof(Index));
            entity.Name = model.Name;
            entity.Slug = model.Slug;
            entity.Country = model.Country;
            entity.CountryOfOrigin = model.CountryOfOrigin;
            entity.Website = model.Website;
            entity.Description = model.Description;
            entity.SortOrder = model.SortOrder;
            entity.IsActive = model.IsActive;
            if (!string.IsNullOrWhiteSpace(nextLogoUrl)) { entity.LogoUrl = nextLogoUrl; entity.Logo = nextLogoUrl; }
            if (!string.IsNullOrWhiteSpace(nextBannerUrl)) entity.BannerUrl = nextBannerUrl;
        }
        else
        {
            model.CreatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(nextLogoUrl)) { model.LogoUrl = nextLogoUrl; model.Logo = nextLogoUrl; }
            if (!string.IsNullOrWhiteSpace(nextBannerUrl)) model.BannerUrl = nextBannerUrl;
            context.Brands.Add(model);
        }
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã lưu thương hiệu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await context.Brands.FirstOrDefaultAsync(x => x.BrandId == id);
        if (entity == null) return RedirectToAction(nameof(Index));
        var productCount = await context.Products.CountAsync(x => x.BrandId == id && (x.Status ?? true));
        if (productCount > 0)
        {
            entity.IsActive = false;
            await context.SaveChangesAsync();
            TempData["Success"] = "Brand có sản phẩm nên đã chuyển sang soft delete (Inactive).";
            return RedirectToAction(nameof(Index));
        }

        if (await context.Products.AnyAsync(p => p.BrandId == id))
        {
            var fallbackBrandId = await context.Brands.AsNoTracking()
                .Where(b => b.BrandId != id)
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.BrandId)
                .Select(b => b.BrandId)
                .FirstOrDefaultAsync();
            if (fallbackBrandId == 0)
            {
                TempData["Error"] = "Còn sản phẩm gắn thương hiệu này nhưng không có thương hiệu khác để chuyển. Tạo thêm một brand rồi thử lại.";
                return RedirectToAction(nameof(Index));
            }

            await context.Products
                .Where(p => p.BrandId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.BrandId, fallbackBrandId));
        }

        context.Brands.Remove(entity);
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã xóa brand.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> SaveUploadAsync(IFormFile? file, string subFolder)
    {
        if (file == null || file.Length == 0) return null;
        var folder = Path.Combine(env.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(folder, fileName);
        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream);
        return $"/uploads/{subFolder}/{fileName}";
    }

    private static string ToSlug(string input)
    {
        return string.Join("-", input.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
