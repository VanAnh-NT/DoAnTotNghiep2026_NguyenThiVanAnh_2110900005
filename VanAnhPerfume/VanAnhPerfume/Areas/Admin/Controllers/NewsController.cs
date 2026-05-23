using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class NewsController(VanAnhPerfumeContext context, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index(string? status, string? tag, string? q, string? sort, int page = 1)
    {
        var query = context.News.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.NewsStatus == status);
        if (!string.IsNullOrWhiteSpace(tag)) query = query.Where(x => x.CategoryTag == tag);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q));
        query = sort switch
        {
            "published" => query.OrderByDescending(x => x.PublishAt ?? DateTime.MinValue),
            "views" => query.OrderByDescending(x => x.ViewCount),
            _ => query.OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
        };
        const int pageSize = 15;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Tags = await context.News.AsNoTracking().Where(x => x.CategoryTag != null && x.CategoryTag != "").Select(x => x.CategoryTag!).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Status = status; ViewBag.Tag = tag; ViewBag.Query = q; ViewBag.Sort = sort; ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(data);
    }

    [HttpGet]
    public IActionResult Create() => View(new News { NewsStatus = "Draft", Status = true, AuthorName = User.Identity?.Name });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(News model, IFormFile? thumbnailFile, string submitAction = "draft")
    {
        await UpsertNewsModelAsync(model, thumbnailFile, null, submitAction);
        if (!ModelState.IsValid) return View(model);
        context.News.Add(model);
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã tạo bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await context.News.FindAsync(id);
        if (entity == null) return NotFound();
        return View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(News model, IFormFile? thumbnailFile, string submitAction = "draft")
    {
        var entity = await context.News.FirstOrDefaultAsync(x => x.NewsId == model.NewsId);
        if (entity == null) return NotFound();
        await UpsertNewsModelAsync(model, thumbnailFile, entity, submitAction);
        if (!ModelState.IsValid) return View(model);
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "File trống" });
        var url = await SaveUpload(file, "news");
        return Json(new { location = url, url });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var entity = await context.News.FirstOrDefaultAsync(x => x.NewsId == id);
        if (entity == null) return Json(new { success = false });
        entity.NewsStatus = entity.NewsStatus == "Published" ? "Draft" : "Published";
        if (entity.NewsStatus == "Published" && !entity.PublishAt.HasValue) entity.PublishAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Json(new { success = true, status = entity.NewsStatus });
    }

    [HttpPost]
    public async Task<IActionResult> BulkPublish(List<int> ids)
    {
        var rows = await context.News.Where(x => ids.Contains(x.NewsId)).ToListAsync();
        foreach (var row in rows)
        {
            row.NewsStatus = "Published";
            row.PublishAt ??= DateTime.UtcNow;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> BulkDelete(List<int> ids)
    {
        var rows = await context.News.Where(x => ids.Contains(x.NewsId)).ToListAsync();
        context.News.RemoveRange(rows);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await context.News.FindAsync(id);
        if (entity != null) { context.News.Remove(entity); await context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    private async Task UpsertNewsModelAsync(News model, IFormFile? thumbnailFile, News? entity, string submitAction)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Slug = ToSlug(string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug);
        if (string.IsNullOrWhiteSpace(model.Title)) ModelState.AddModelError(nameof(model.Title), "Tiêu đề là bắt buộc.");
        if (string.IsNullOrWhiteSpace(model.Content)) ModelState.AddModelError(nameof(model.Content), "Nội dung là bắt buộc.");
        if (await context.News.AnyAsync(x => x.Slug == model.Slug && x.NewsId != model.NewsId)) ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");
        if (!ModelState.IsValid) return;

        model.NewsStatus = submitAction == "publish" ? "Published" : "Draft";
        if (model.NewsStatus == "Published" && !model.PublishAt.HasValue) model.PublishAt = DateTime.UtcNow;
        model.Excerpt = string.IsNullOrWhiteSpace(model.Excerpt) ? BuildExcerpt(model.Content, 150) : model.Excerpt.Trim();
        model.MetaTitle = string.IsNullOrWhiteSpace(model.MetaTitle) ? model.Title : model.MetaTitle.Trim();
        model.MetaDescription = string.IsNullOrWhiteSpace(model.MetaDescription) ? model.Excerpt : model.MetaDescription.Trim();
        if (model.MetaDescription != null && model.MetaDescription.Length > 2000)
            model.MetaDescription = model.MetaDescription[..2000];
        model.AuthorName = string.IsNullOrWhiteSpace(model.AuthorName) ? (User.Identity?.Name ?? "Admin") : model.AuthorName.Trim();

        if (thumbnailFile != null && thumbnailFile.Length > 0) model.ThumbnailUrl = await SaveUpload(thumbnailFile, "news");
        if (entity == null)
        {
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.ViewCount = 0;
            model.Status = true;
            return;
        }
        entity.Title = model.Title;
        entity.Slug = model.Slug;
        entity.Content = model.Content;
        entity.Excerpt = model.Excerpt;
        entity.AuthorName = model.AuthorName;
        entity.CategoryTag = model.CategoryTag;
        entity.NewsStatus = model.NewsStatus;
        entity.PublishAt = model.PublishAt;
        entity.IsFeatured = model.IsFeatured;
        entity.MetaTitle = model.MetaTitle;
        entity.MetaDescription = model.MetaDescription;
        entity.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(model.ThumbnailUrl)) entity.ThumbnailUrl = model.ThumbnailUrl;
    }

    private async Task<string> SaveUpload(IFormFile file, string folderName)
    {
        var folder = Path.Combine(env.WebRootPath, "uploads", folderName);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(folder, fileName);
        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs);
        return $"/uploads/{folderName}/{fileName}";
    }

    private static string BuildExcerpt(string? html, int length)
    {
        var plain = Regex.Replace(html ?? string.Empty, "<.*?>", " ");
        plain = Regex.Replace(plain, @"\s+", " ").Trim();
        return plain.Length <= length ? plain : plain[..length] + "...";
    }

    private static string ToSlug(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        var cleaned = new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        cleaned = Regex.Replace(cleaned, @"[^a-z0-9\s-]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", "-");
        return cleaned.Trim('-');
    }
}
