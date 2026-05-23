using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CouponController(VanAnhPerfumeContext context) : Controller
{
    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = context.Coupons.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.Code.Contains(q));
        }
        const int pageSize = 15;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var coupons = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        return View(coupons);
    }

    [HttpPost]
    public IActionResult Create(Coupon model)
    {
        TempData["Error"] = "Coupon đã chuyển sang chế độ legacy. Vui lòng quản lý khuyến mãi ở mục Promotion.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ToggleActive(int id)
    {
        TempData["Error"] = "Coupon legacy không còn được bật/tắt từ admin.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        TempData["Error"] = "Coupon legacy không còn được xóa từ admin.";
        return RedirectToAction(nameof(Index));
    }
}
