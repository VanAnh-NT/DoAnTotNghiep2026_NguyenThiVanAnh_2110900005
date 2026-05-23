using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoryController(VanAnhPerfumeContext context, IWebHostEnvironment env) : Controller
{
    public async Task<IActionResult> Index()
    {
        var categories = await context.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        var productCounts = await context.Products.AsNoTracking()
            .Where(p => p.Status ?? true)
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
        ViewBag.ProductCounts = BuildCountMap(categories, productCounts);
        ViewBag.Parents = categories.Where(x => x.ParentId == null).ToList();
        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await context.Categories.AsNoTracking()
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        ViewBag.Parents = categories;

        return View("CreateEdit", new Category { IsActive = true, SortOrder = 0 });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await context.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryId == id);
        if (entity == null) return NotFound();

        var parents = await context.Categories.AsNoTracking()
            .Where(x => x.ParentId == null && x.CategoryId != id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        ViewBag.Parents = parents;

        return View("CreateEdit", entity);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Category model, IFormFile? imageFile)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Slug = ToSlug(string.IsNullOrWhiteSpace(model.Slug) ? model.Name : model.Slug);
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["Error"] = "Tên danh mục là bắt buộc.";
            return RedirectToAction(nameof(Index));
        }
        if (await context.Categories.AnyAsync(x => x.Slug == model.Slug && x.CategoryId != model.CategoryId))
        {
            TempData["Error"] = "Slug đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }
        if (model.ParentId.HasValue)
        {
            var parent = await context.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.CategoryId == model.ParentId.Value);
            if (parent == null || parent.ParentId != null || parent.CategoryId == model.CategoryId)
            {
                TempData["Error"] = "Danh mục cha không hợp lệ (chỉ hỗ trợ 2 cấp).";
                return RedirectToAction(nameof(Index));
            }
        }
        if (imageFile != null && imageFile.Length > 0)
        {
            var folder = Path.Combine(env.WebRootPath, "uploads", "categories");
            Directory.CreateDirectory(folder);
            var name = $"{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
            var fullPath = Path.Combine(folder, name);
            await using var stream = System.IO.File.Create(fullPath);
            await imageFile.CopyToAsync(stream);
            model.ImageUrl = $"/uploads/categories/{name}";
        }

        if (model.CategoryId > 0)
        {
            var entity = await context.Categories.FirstOrDefaultAsync(x => x.CategoryId == model.CategoryId);
            if (entity == null) return RedirectToAction(nameof(Index));
            entity.Name = model.Name;
            entity.Slug = model.Slug;
            entity.Description = model.Description;
            entity.ParentId = model.ParentId;
            entity.SortOrder = model.SortOrder;
            entity.IsActive = model.IsActive;
            if (!string.IsNullOrWhiteSpace(model.ImageUrl)) entity.ImageUrl = model.ImageUrl;
        }
        else
        {
            model.CreatedAt = DateTime.UtcNow;
            context.Categories.Add(model);
        }
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã lưu danh mục.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await context.Categories.FirstOrDefaultAsync(x => x.CategoryId == id);
        if (entity == null) return Json(new { success = false });
        entity.IsActive = !entity.IsActive;
        await context.SaveChangesAsync();
        return Json(new { success = true, isActive = entity.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSort([FromBody] List<CategorySortInput> items)
    {
        var ids = items.Select(x => x.Id).ToHashSet();
        var entities = await context.Categories.Where(x => ids.Contains(x.CategoryId)).ToListAsync();
        foreach (var entity in entities)
        {
            var input = items.First(x => x.Id == entity.CategoryId);
            entity.SortOrder = input.Order;
        }
        await context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, bool cascade = false)
    {
        var entity = await context.Categories.FirstOrDefaultAsync(x => x.CategoryId == id);
        if (entity == null) return RedirectToAction(nameof(Index));
        var childIds = await context.Categories.Where(x => x.ParentId == id).Select(x => x.CategoryId).ToListAsync();
        var scopedIds = new List<int> { id };
        scopedIds.AddRange(childIds);
        // Chỉ chặn xóa khi còn sản phẩm đang hoạt động; SP đã "xóa" (soft) không tính.
        var productCount = await context.Products.CountAsync(x => scopedIds.Contains(x.CategoryId) && (x.Status ?? true));
        if (productCount > 0)
        {
            TempData["Error"] = $"Danh mục đang có {productCount} sản phẩm đang hoạt động, không thể xóa";
            return RedirectToAction(nameof(Index));
        }
        if (childIds.Count > 0 && !cascade)
        {
            TempData["Error"] = "Danh mục cha có danh mục con. Chọn xóa kèm danh mục con.";
            return RedirectToAction(nameof(Index));
        }
        var removeIds = cascade ? scopedIds : [id];

        // Sản phẩm đã "xóa" (soft) vẫn còn CategoryId — FK ở SQL vẫn chặn xóa danh mục. Chuyển sang danh mục khác trước khi xóa hàng Categories.
        if (await context.Products.AnyAsync(p => removeIds.Contains(p.CategoryId)))
        {
            var fallbackCategoryId = await context.Categories.AsNoTracking()
                .Where(c => !removeIds.Contains(c.CategoryId))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CategoryId)
                .Select(c => c.CategoryId)
                .FirstOrDefaultAsync();
            if (fallbackCategoryId == 0)
            {
                TempData["Error"] = "Còn sản phẩm gắn danh mục này nhưng không có danh mục khác để chuyển. Hãy tạo thêm ít nhất một danh mục khác rồi thử lại.";
                return RedirectToAction(nameof(Index));
            }

            await context.Products
                .Where(p => removeIds.Contains(p.CategoryId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CategoryId, fallbackCategoryId));
        }

        var entities = await context.Categories.Where(x => removeIds.Contains(x.CategoryId)).ToListAsync();
        context.Categories.RemoveRange(entities);
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã xóa danh mục.";
        return RedirectToAction(nameof(Index));
    }

    private static Dictionary<int, int> BuildCountMap(List<Category> allCategories, Dictionary<int, int> directCounts)
    {
        var childrenMap = allCategories.Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToList());
        var result = new Dictionary<int, int>();
        foreach (var category in allCategories)
        {
            var count = directCounts.GetValueOrDefault(category.CategoryId);
            if (childrenMap.TryGetValue(category.CategoryId, out var children))
            {
                count += children.Sum(childId => directCounts.GetValueOrDefault(childId));
            }
            result[category.CategoryId] = count;
        }
        return result;
    }

    private static string ToSlug(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        return string.Join("-", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public class CategorySortInput
{
    public int Id { get; set; }
    public int Order { get; set; }
}
