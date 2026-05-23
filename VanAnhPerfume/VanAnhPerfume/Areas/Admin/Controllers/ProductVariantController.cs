using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ProductVariantController(VanAnhPerfumeContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Products = await context.Products.OrderBy(x => x.Name).ToListAsync();
        var variants = await context.ProductVariants.Include(v => v.Product).OrderByDescending(x => x.VariantId).ToListAsync();
        return View(variants);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductVariant model)
    {
        if (ModelState.IsValid)
        {
            context.ProductVariants.Add(model);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var variant = await context.ProductVariants.FindAsync(id);
        if (variant != null)
        {
            context.ProductVariants.Remove(variant);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
