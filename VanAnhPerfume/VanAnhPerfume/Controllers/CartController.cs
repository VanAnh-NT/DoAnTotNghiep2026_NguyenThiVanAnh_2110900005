using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Helpers;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels;
namespace VanAnhPerfume.Controllers
{
    public class CartController(VanAnhPerfumeContext context) : Controller
    {
        private const string CART_KEY = "VanAnhCart";
        private const string PROMOTION_KEY = "VanAnhPromotion";
        private const string BUY_NOW_BACKUP_KEY = "VanAnhCartBuyNowBackup";
        private const string BUY_NOW_ACTIVE_KEY = "VanAnhBuyNowActive";

        // 1. TRANG GIỎ HÀNG
        [HttpGet("/Cart/Index")]
        [HttpGet("/gio-hang")]
        public IActionResult Index()
        {
            TryRestoreCartAfterAbandonedBuyNow();
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            return View(cart);
        }

        /// <summary>
        /// Khôi phục giỏ trước khi "Mua ngay" nếu khách vào lại trang giỏ mà chưa hoàn tất thanh toán.
        /// </summary>
        private void TryRestoreCartAfterAbandonedBuyNow()
        {
            if (HttpContext.Session.GetString(BUY_NOW_ACTIVE_KEY) != "1")
                return;

            var backup = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(BUY_NOW_BACKUP_KEY) ?? [];
            HttpContext.Session.SetObjectAsJson(CART_KEY, backup);
            HttpContext.Session.Remove(BUY_NOW_BACKUP_KEY);
            HttpContext.Session.Remove(BUY_NOW_ACTIVE_KEY);
            HttpContext.Session.Remove(PROMOTION_KEY);
        }

        /// <summary>
        /// Mua ngay: đưa đúng sản phẩm đang chọn vào session và chuyển thẳng tới thanh toán (không cần thêm giỏ trước).
        /// Giỏ hàng trước đó được giữ tạm để khôi phục nếu khách mở lại trang giỏ trước khi đặt hàng xong.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BuyNow(int variantId, int quantity = 1)
        {
            if (quantity < 1)
                quantity = 1;

            var variant = await context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Inventory)
                .FirstOrDefaultAsync(v => v.VariantId == variantId);

            if (variant == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction("Index", "Home");
            }

            var available = variant.Inventory != null
                ? Math.Max(0, variant.Inventory.QuantityAvailable - variant.Inventory.QuantityReserved)
                : variant.Stock;
            if (available < quantity)
            {
                TempData["Error"] = $"Sản phẩm chỉ còn {available} sản phẩm.";
                return RedirectToAction("Detail", "Product", new { id = variant.ProductId });
            }

            var previousCart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? [];
            if (previousCart.Count > 0)
            {
                HttpContext.Session.SetObjectAsJson(BUY_NOW_BACKUP_KEY, previousCart);
                HttpContext.Session.SetString(BUY_NOW_ACTIVE_KEY, "1");
            }
            else
            {
                HttpContext.Session.Remove(BUY_NOW_BACKUP_KEY);
                HttpContext.Session.Remove(BUY_NOW_ACTIVE_KEY);
            }

            HttpContext.Session.Remove(PROMOTION_KEY);

            var single = new List<CartItemVM>
            {
                new()
                {
                    VariantId = variant.VariantId,
                    ProductId = variant.ProductId,
                    ProductName = variant.Product.Name,
                    Size = variant.Size,
                    Price = variant.Price,
                    Quantity = quantity,
                    ImageUrl = variant.Product.MainImage
                }
            };
            HttpContext.Session.SetObjectAsJson(CART_KEY, single);

            context.AddToCartLogs.Add(new AddToCartLog
            {
                ProductId = variant.ProductId,
                VariantId = variantId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            return RedirectToAction("Index", "Checkout");
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon([FromBody] CouponRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return Json(new { success = false, message = "Mã khuyến mãi không hợp lệ." });
            }

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            var subtotal = cart.Sum(x => x.Total);
            var code = request.Code.Trim();

            var promotion = await context.Promotions.FirstOrDefaultAsync(x =>
                x.IsActive == true
                && x.StartDate <= DateTime.UtcNow
                && x.EndDate >= DateTime.UtcNow
                && x.Name == code);

            if (promotion == null)
            {
                return Json(new { success = false, message = "Mã khuyến mãi đã hết hạn hoặc không khả dụng." });
            }

            if (subtotal < (promotion.MinOrderValue ?? 0))
            {
                return Json(new { success = false, message = $"Đơn tối thiểu {(promotion.MinOrderValue ?? 0):N0} ₫ để dùng mã này." });
            }

            var discountAmount = Math.Min(subtotal, subtotal * (promotion.DiscountPercent / 100m));

            HttpContext.Session.SetObjectAsJson(PROMOTION_KEY, new CheckoutVM
            {
                PromotionId = promotion.PromoId,
                PromotionCode = promotion.Name,
                DiscountAmount = discountAmount
            });

            var shipping = subtotal >= 500000 ? 0 : 30000;
            var finalTotal = Math.Max(0, subtotal + shipping - discountAmount);

            return Json(new
            {
                success = true,
                subtotal,
                shipping,
                discountAmount,
                total = finalTotal,
                message = "Áp dụng mã khuyến mãi thành công."
            });
        }

        private async Task<decimal> RecalculateDiscountAsync(decimal subtotal)
        {
            var state = HttpContext.Session.GetObjectFromJson<CheckoutVM>(PROMOTION_KEY);
            if (state?.PromotionId == null) return 0m;

            var promotion = await context.Promotions.FirstOrDefaultAsync(x =>
                x.PromoId == state.PromotionId
                && x.IsActive == true
                && x.StartDate <= DateTime.UtcNow
                && x.EndDate >= DateTime.UtcNow);

            if (promotion == null || subtotal < (promotion.MinOrderValue ?? 0))
            {
                HttpContext.Session.Remove(PROMOTION_KEY);
                return 0m;
            }

            var discountAmount = Math.Min(subtotal, subtotal * (promotion.DiscountPercent / 100m));
            state.DiscountAmount = discountAmount;
            state.PromotionCode = promotion.Name;
            HttpContext.Session.SetObjectAsJson(PROMOTION_KEY, state);
            return discountAmount;
        }

        [HttpGet]
        public IActionResult MiniCart()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            return PartialView("_MiniCartSidebar", new MiniCartVM { Items = cart });
        }

        [HttpGet]
        public IActionResult CartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            return Json(new { count = cart.Sum(x => x.Quantity), total = cart.Sum(x => x.Total) });
        }

        // 2. THÊM VÀO GIỎ HÀNG
        [HttpPost]
        public async Task<IActionResult> AddToCart(int variantId, int quantity = 1)
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            var item = cart.FirstOrDefault(c => c.VariantId == variantId);

            if (item != null)
            {
                item.Quantity += quantity;
            }
            else
            {
                var variant = await context.ProductVariants
                    .Include(v => v.Product)
                    .Include(v => v.Inventory)
                    .FirstOrDefaultAsync(v => v.VariantId == variantId);

                if (variant != null)
                {
                    var available = variant.Inventory != null
                        ? Math.Max(0, variant.Inventory.QuantityAvailable - variant.Inventory.QuantityReserved)
                        : variant.Stock;
                    if (available < quantity)
                    {
                        return Json(new { success = false, message = $"Sản phẩm chỉ còn {available} sản phẩm." });
                    }
                    cart.Add(new CartItemVM
                    {
                        VariantId = variant.VariantId,
                        ProductId = variant.ProductId,
                        ProductName = variant.Product.Name,
                        Size = variant.Size,
                        Price = variant.Price,
                        Quantity = quantity,
                        ImageUrl = variant.Product.MainImage
                    });
                }
            }
            HttpContext.Session.SetObjectAsJson(CART_KEY, cart);
            context.AddToCartLogs.Add(new AddToCartLog
            {
                ProductId = item?.ProductId,
                VariantId = variantId,
                Quantity = quantity,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Đã thêm vào giỏ hàng.", cartCount = cart.Sum(x => x.Quantity) });
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public Task<IActionResult> Add(int variantId, int quantity = 1) => AddToCart(variantId, quantity);

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int variantId, int quantity)
        {
            if (quantity < 1)
            {
                quantity = 1;
            }

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            var item = cart.FirstOrDefault(c => c.VariantId == variantId);
            if (item == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm trong giỏ." });
            }

            var stock = await context.ProductVariants
                .Include(v => v.Inventory)
                .Where(v => v.VariantId == variantId)
                .Select(v => v.Inventory != null ? v.Inventory.QuantityAvailable - v.Inventory.QuantityReserved : v.Stock)
                .FirstOrDefaultAsync();

            if (stock <= 0)
            {
                return Json(new { success = false, message = "Biến thể sản phẩm không khả dụng." });
            }

            item.Quantity = Math.Min(quantity, stock);
            HttpContext.Session.SetObjectAsJson(CART_KEY, cart);

            var subtotal = cart.Sum(x => x.Total);
            var shipping = subtotal >= 500000 ? 0 : 30000;
            var discountAmount = await RecalculateDiscountAsync(subtotal);
            var total = Math.Max(0, subtotal + shipping - discountAmount);

            return Json(new
            {
                success = true,
                quantity = item.Quantity,
                itemTotal = item.Total,
                subtotal,
                shipping,
                discountAmount,
                total,
                cartCount = cart.Sum(x => x.Quantity)
            });
        }

        // 3. XÓA KHỎI GIỎ HÀNG
        [HttpPost]
        public async Task<IActionResult> Remove(int id)
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            cart.RemoveAll(c => c.VariantId == id);
            HttpContext.Session.SetObjectAsJson(CART_KEY, cart);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var subtotal = cart.Sum(x => x.Total);
                var shipping = subtotal >= 500000 ? 0 : 30000;
                var discountAmount = await RecalculateDiscountAsync(subtotal);
                var total = Math.Max(0, subtotal + shipping - discountAmount);
                return Json(new
                {
                    success = true,
                    count = cart.Sum(x => x.Quantity),
                    subtotal,
                    shipping,
                    discountAmount,
                    total,
                    isEmpty = cart.Count == 0
                });
            }
            return RedirectToAction("Index");
        }

        // 4. TRANG THANH TOÁN
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            if (cart.Count == 0) return RedirectToAction("Index");
            return RedirectToAction("Index", "Checkout");
        }

        public class CouponRequest
        {
            public string Code { get; set; } = string.Empty;
        }
    }
}