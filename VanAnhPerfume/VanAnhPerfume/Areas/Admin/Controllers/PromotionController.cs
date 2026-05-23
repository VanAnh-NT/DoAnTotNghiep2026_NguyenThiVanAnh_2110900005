using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using VanAnhPerfume.Data;

using VanAnhPerfume.Models.Entities;

using VanAnhPerfume.Models.ViewModels;



namespace VanAnhPerfume.Areas.Admin.Controllers;



[Area("Admin")]

[Authorize(Roles = "Admin")]

public class PromotionController(VanAnhPerfumeContext context) : Controller

{

    public async Task<IActionResult> Index(string? q, int page = 1)

    {

        var query = context.Promotions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))

        {

            query = query.Where(x => x.Name.Contains(q));

        }

        const int pageSize = 15;

        var totalItems = await query.CountAsync();

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

        page = Math.Clamp(page, 1, totalPages);

        var items = await query.OrderByDescending(x => x.PromoId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Query = q;

        ViewBag.Page = page;

        ViewBag.TotalPages = totalPages;

        return View(items);

    }



    [HttpGet]

    public IActionResult Create() =>

        View(new PromotionFormVm

        {

            IsActive = true,

            DiscountPercent = 10,

            StartDate = DateTime.Today,

            EndDate = DateTime.Today.AddDays(7)

        });



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Save(PromotionFormVm vm)

    {

        var isActiveValues = Request.Form["IsActive"];

        if (isActiveValues.Count > 0)

        {

            vm.IsActive = isActiveValues.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));

        }



        if (!ModelState.IsValid)

            return View("Create", vm);



        var model = vm.ToEntity();



        if (await context.Promotions.AnyAsync(x => x.Name == model.Name && x.StartDate.Date == model.StartDate.Date && x.PromoId != model.PromoId))

        {

            ModelState.AddModelError(nameof(vm.Name), "Đã có khuyến mãi cùng tên và cùng ngày bắt đầu.");

            return View("Create", vm);

        }



        if (model.PromoId > 0)

        {

            var entity = await context.Promotions.FirstOrDefaultAsync(x => x.PromoId == model.PromoId);

            if (entity == null) return NotFound();



            entity.Name = model.Name;

            entity.DiscountPercent = model.DiscountPercent;

            entity.StartDate = model.StartDate;

            entity.EndDate = model.EndDate;

            entity.MinOrderValue = model.MinOrderValue;

            entity.ApplicableCategoryIds = model.ApplicableCategoryIds;

            entity.ApplicableProductIds = model.ApplicableProductIds;

            entity.AutoApply = model.AutoApply;

            entity.IsActive = model.IsActive;



            await context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật khuyến mãi.";

            return RedirectToAction(nameof(Index));

        }



        context.Promotions.Add(model);

        await context.SaveChangesAsync();

        TempData["Success"] = "Đã tạo khuyến mãi.";

        return RedirectToAction(nameof(Index));

    }



    [HttpGet]

    public async Task<IActionResult> Edit(int id)

    {

        var entity = await context.Promotions.FirstOrDefaultAsync(x => x.PromoId == id);

        if (entity == null) return NotFound();

        return View("Create", PromotionFormVm.FromEntity(entity));

    }



    [HttpPost]

    public async Task<IActionResult> Delete(int id)

    {

        var promo = await context.Promotions.FindAsync(id);

        if (promo != null)

        {

            context.Promotions.Remove(promo);

            await context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa khuyến mãi.";

        }

        return RedirectToAction(nameof(Index));

    }

}


