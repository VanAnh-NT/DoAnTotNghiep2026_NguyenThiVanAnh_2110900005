using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BannerController(VanAnhPerfumeContext context, IWebHostEnvironment env, IMemoryCache memoryCache) : Controller
{
    private void InvalidateHomeBannersCache()
    {
        memoryCache.Remove("home_banners_hero");
        memoryCache.Remove("home_banners_mid");
    }

    public async Task<IActionResult> Index(string? position)
    {
        var query = context.Banners.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(position)) query = query.Where(x => x.Position == position);
        var data = await query.OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id).ToListAsync();
        ViewBag.Position = position;
        return View(data);
    }

    [HttpGet]
    public IActionResult Create() => View("CreateEdit", new Banner { Position = "hero", SortOrder = 1, IsActive = true });

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await context.Banners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        return View("CreateEdit", entity);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Banner model, IFormFile? imageFile, IFormFile? mobileImageFile)
    {
        if (imageFile != null && imageFile.Length > 0) model.ImageUrl = await SaveUpload(imageFile);
        if (mobileImageFile != null && mobileImageFile.Length > 0) model.MobileImageUrl = await SaveUpload(mobileImageFile);
        model.ButtonText = string.IsNullOrWhiteSpace(model.ButtonText) ? "Khám phá ngay" : model.ButtonText;
        model.TextColor = string.IsNullOrWhiteSpace(model.TextColor) ? "#FFFFFF" : model.TextColor;
        if (model.Id > 0)
        {
            var row = await context.Banners.FirstOrDefaultAsync(x => x.Id == model.Id);
            if (row == null) return RedirectToAction(nameof(Index));
            row.Title = model.Title; row.SubTitle = model.SubTitle; row.Description = model.Description; row.LinkUrl = model.LinkUrl;
            row.ButtonText = model.ButtonText; row.TextColor = model.TextColor; row.SortOrder = model.SortOrder; row.IsActive = model.IsActive;
            row.DisplayFrom = model.DisplayFrom; row.DisplayTo = model.DisplayTo; row.Position = model.Position;
            if (!string.IsNullOrWhiteSpace(model.ImageUrl)) row.ImageUrl = model.ImageUrl;
            if (!string.IsNullOrWhiteSpace(model.MobileImageUrl)) row.MobileImageUrl = model.MobileImageUrl;
        }
        else
        {
            model.CreatedAt = DateTime.UtcNow;
            context.Banners.Add(model);
        }
        await context.SaveChangesAsync();
        InvalidateHomeBannersCache();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var banner = await context.Banners.FindAsync(id);
        if (banner == null) return Json(new { success = false });
        banner.IsActive = !banner.IsActive;
        await context.SaveChangesAsync();
        InvalidateHomeBannersCache();
        return Json(new { success = true, isActive = banner.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSort([FromBody] List<BannerSortInput> items)
    {
        var ids = items.Select(x => x.Id).ToHashSet();
        var rows = await context.Banners.Where(x => ids.Contains(x.Id)).ToListAsync();
        foreach (var row in rows) row.SortOrder = items.First(x => x.Id == row.Id).Order;
        await context.SaveChangesAsync();
        InvalidateHomeBannersCache();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var banner = await context.Banners.FindAsync(id);
        if (banner != null)
        {
            context.Banners.Remove(banner);
            await context.SaveChangesAsync();
            InvalidateHomeBannersCache();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveUpload(IFormFile file)
    {
        var folder = Path.Combine(env.WebRootPath, "uploads", "banners");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(folder, fileName);
        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);
        return $"/uploads/banners/{fileName}";
    }
}

public class BannerSortInput
{
    public int Id { get; set; }
    public int Order { get; set; }
}
