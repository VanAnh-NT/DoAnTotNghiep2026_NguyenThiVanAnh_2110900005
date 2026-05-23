using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Helpers;
using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Controllers;

public class WishlistController(VanAnhPerfumeContext context) : Controller
{
    private const string WishlistKey = "VanAnhWishlist";

    public IActionResult Index()
    {
        var wishlist = HttpContext.Session.GetObjectFromJson<List<WishlistItemVM>>(WishlistKey) ?? [];
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Profile", "Account", new { tab = "wishlist" });
        return View(wishlist);
    }

    [HttpGet]
    public IActionResult Count()
    {
        var wishlist = HttpContext.Session.GetObjectFromJson<List<WishlistItemVM>>(WishlistKey) ?? [];
        return Json(new { count = wishlist.Count });
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId)
    {
        var wishlist = HttpContext.Session.GetObjectFromJson<List<WishlistItemVM>>(WishlistKey) ?? [];
        var exists = wishlist.Any(x => x.ProductId == productId);
        if (!exists)
        {
            var product = await context.Products
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product != null)
            {
                wishlist.Add(new WishlistItemVM
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    ImageUrl = product.MainImage,
                    Price = product.ProductVariants.OrderBy(v => v.Price).Select(v => v.Price).FirstOrDefault()
                });
                HttpContext.Session.SetObjectAsJson(WishlistKey, wishlist);
            }
        }
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, message = "Đã thêm vào danh sách yêu thích.", wishlistCount = wishlist.Count });
        }
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Remove(int productId, string? returnUrl = null)
    {
        var wishlist = HttpContext.Session.GetObjectFromJson<List<WishlistItemVM>>(WishlistKey) ?? [];
        wishlist.RemoveAll(x => x.ProductId == productId);
        HttpContext.Session.SetObjectAsJson(WishlistKey, wishlist);
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
