using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;

namespace VanAnhPerfume.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MainController(VanAnhPerfumeContext context) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var utcNow = DateTime.UtcNow;
            var today = utcNow.Date;
            var yesterday = today.AddDays(-1);
            var startThisMonth = new DateTime(today.Year, today.Month, 1);
            var startLastMonth = startThisMonth.AddMonths(-1);

            var totalRevenue = await context.Orders.SumAsync(o => o.TotalAmount);
            var totalOrders = await context.Orders.CountAsync();
            var totalCustomers = await context.Users.CountAsync(u => u.RoleId == 2);
            var totalProducts = await context.Products.CountAsync();
            var todayRevenue = await context.Orders
                .Where(x => x.OrderDate.HasValue && x.OrderDate.Value.Date == today)
                .SumAsync(x => x.TotalAmount);
            var yesterdayRevenue = await context.Orders
                .Where(x => x.OrderDate.HasValue && x.OrderDate.Value.Date == yesterday)
                .SumAsync(x => x.TotalAmount);
            var thisMonthRevenue = await context.Orders
                .Where(x => x.OrderDate.HasValue && x.OrderDate.Value >= startThisMonth)
                .SumAsync(x => x.TotalAmount);
            var lastMonthRevenue = await context.Orders
                .Where(x => x.OrderDate.HasValue && x.OrderDate.Value >= startLastMonth && x.OrderDate.Value < startThisMonth)
                .SumAsync(x => x.TotalAmount);

            var thisMonthOrders = await context.Orders
                .CountAsync(x => x.OrderDate.HasValue && x.OrderDate.Value >= startThisMonth);
            var aov = thisMonthOrders > 0 ? thisMonthRevenue / thisMonthOrders : 0;
            var thisMonthAddToCart = await context.AddToCartLogs
                .CountAsync(x => x.CreatedAt >= startThisMonth);
            var conversionRate = thisMonthAddToCart > 0 ? (decimal)thisMonthOrders / thisMonthAddToCart : 0;

            var lowStockProducts = await context.ProductVariants
                .Select(x => new
                {
                    x.Product.Name,
                    x.Size,
                    x.Sku,
                    // Avoid Math.Max with nested conditionals so EF can translate this query.
                    Sellable = (x.Inventory != null ? x.Inventory.QuantityAvailable : x.Stock)
                              - (x.Inventory != null ? x.Inventory.QuantityReserved : 0),
                    Threshold = x.Inventory != null ? x.Inventory.LowStockThreshold : 5
                })
                .Where(x => x.Sellable > 0 && x.Sellable <= x.Threshold)
                .OrderBy(x => x.Sellable)
                .Take(5)
                .ToListAsync();
            var stalePendingOrders = await context.Orders
                .Where(x => x.Status == "Pending" && x.OrderDate.HasValue && x.OrderDate.Value <= utcNow.AddHours(-24))
                .OrderBy(x => x.OrderDate)
                .Take(8)
                .Select(x => new { x.OrderId, x.OrderDate, x.FullName })
                .ToListAsync();

            var recentOrders = await context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalRevenue = totalRevenue.ToString("N0") + " ₫";
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalCustomers = totalCustomers;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.TodayRevenue = todayRevenue;
            ViewBag.YesterdayRevenue = yesterdayRevenue;
            ViewBag.ThisMonthRevenue = thisMonthRevenue;
            ViewBag.LastMonthRevenue = lastMonthRevenue;
            ViewBag.Aov = aov;
            ViewBag.ConversionRate = conversionRate;
            ViewBag.LowStockProducts = lowStockProducts;
            ViewBag.StalePendingOrders = stalePendingOrders;

            return View(recentOrders);
        }

        [HttpGet]
        public async Task<IActionResult> RevenueChart()
        {
            var from = DateTime.UtcNow.Date.AddDays(-29);
            var grouped = await context.Orders
                .Where(x => x.OrderDate.HasValue && x.OrderDate.Value.Date >= from)
                .GroupBy(x => x.OrderDate!.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount)
                })
                .ToListAsync();

            var points = Enumerable.Range(0, 30)
                .Select(offset => from.AddDays(offset))
                .Select(d => new
                {
                    date = d.ToString("dd/MM"),
                    revenue = grouped.FirstOrDefault(x => x.Date == d)?.Revenue ?? 0
                });

            return Json(points);
        }

        [HttpGet]
        public async Task<IActionResult> TopProducts()
        {
            var top = await context.OrderDetails
                .Include(x => x.Variant)
                    .ThenInclude(x => x.Product)
                .GroupBy(x => new { x.Variant.ProductId, x.Variant.Product.Name })
                .Select(g => new
                {
                    productName = g.Key.Name,
                    qty = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.qty)
                .Take(5)
                .ToListAsync();

            return Json(top);
        }

        [HttpGet]
        public async Task<IActionResult> RevenueByCategory()
        {
            var data = await context.OrderDetails
                .Include(x => x.Variant)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Category)
                .GroupBy(x => x.Variant.Product.Category.Name)
                .Select(g => new { label = g.Key, revenue = g.Sum(x => x.Price * x.Quantity) })
                .OrderByDescending(x => x.revenue)
                .Take(8)
                .ToListAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> RevenueByBrand()
        {
            var data = await context.OrderDetails
                .Include(x => x.Variant)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Brand)
                .GroupBy(x => x.Variant.Product.Brand.Name)
                .Select(g => new { label = g.Key, revenue = g.Sum(x => x.Price * x.Quantity) })
                .OrderByDescending(x => x.revenue)
                .Take(8)
                .ToListAsync();
            return Json(data);
        }
    }
}