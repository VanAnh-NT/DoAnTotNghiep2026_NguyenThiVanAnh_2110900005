using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CustomerController(VanAnhPerfumeContext context) : Controller
{
    private const string PaidStatus = "Paid";
    private const string CompletedStatus = "Completed";
    private const string CancelledStatus = "Cancelled";

    public async Task<IActionResult> Index(
        string? q,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        string sort = "created_desc",
        int page = 1)
    {
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var statsBase = context.Users.AsNoTracking().Where(u => u.RoleId == 2);
        var stats = new AdminCustomerStatsVm
        {
            Total = await statsBase.CountAsync(),
            Active = await statsBase.CountAsync(u => u.IsActive),
            Locked = await statsBase.CountAsync(u => !u.IsActive),
            NewThisMonth = await statsBase.CountAsync(u => u.CreatedAt >= monthStart)
        };

        var query = context.Users.AsNoTracking().Where(u => u.RoleId == 2);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive);
            else if (status.Equals("locked", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => !u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(u =>
                u.FullName.Contains(t) ||
                u.Email.Contains(t) ||
                (u.Phone != null && u.Phone.Contains(t)));
        }

        if (fromDate.HasValue)
            query = query.Where(u => u.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue)
            query = query.Where(u => u.CreatedAt < toDate.Value.Date.AddDays(1));

        var rowQuery = query.Select(u => new AdminCustomerRowVm
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Avatar = u.Avatar,
            OrderCount = context.Orders.Count(o => o.UserId == u.UserId),
            PaidSpend = context.Orders
                .Where(o => o.UserId == u.UserId && o.PaymentStatus == PaidStatus)
                .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            IsActive = u.IsActive
        });

        rowQuery = sort switch
        {
            "spent_desc" => rowQuery
                .OrderByDescending(x => x.PaidSpend)
                .ThenByDescending(x => x.UserId),
            "orders_desc" => rowQuery
                .OrderByDescending(x => x.OrderCount)
                .ThenByDescending(x => x.UserId),
            _ => rowQuery
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.UserId)
        };

        const int pageSize = 20;
        var totalItems = await rowQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rows = await rowQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new AdminCustomerIndexVm
        {
            Stats = stats,
            Rows = rows,
            Page = page,
            TotalPages = totalPages,
            Q = q,
            StatusFilter = status,
            FromDate = fromDate,
            ToDate = toDate,
            Sort = sort
        };

        return View(vm);
    }

    public async Task<IActionResult> Detail(int id, int orderPage = 1)
    {
        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == id && x.RoleId == 2);
        if (user == null)
            return NotFound();

        var addresses = await context.AddressBooks.AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        var ordersQuery = context.Orders.AsNoTracking().Where(o => o.UserId == id);

        var totalOrders = await ordersQuery.CountAsync();
        var completedOrders = await ordersQuery.CountAsync(o => o.Status == CompletedStatus);
        var cancelledOrders = await ordersQuery.CountAsync(o => o.Status == CancelledStatus);
        var totalPaidSpend = await ordersQuery
            .Where(o => o.PaymentStatus == PaidStatus)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var tierSpend = await ordersQuery
            .Where(o => o.Status == CompletedStatus && o.PaymentStatus == PaidStatus)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var completionRate = totalOrders == 0
            ? 0
            : Math.Round(100.0 * completedOrders / totalOrders, 1);

        var reviewedProductCount = await context.Reviews.AsNoTracking()
            .CountAsync(r => r.UserId == id);

        var avgOrder = totalOrders == 0 ? 0m : Math.Round(totalPaidSpend / totalOrders, 0);

        const int orderPageSize = 10;
        var ordersTotalPages = Math.Max(1, (int)Math.Ceiling(totalOrders / (double)orderPageSize));
        orderPage = Math.Clamp(orderPage, 1, ordersTotalPages);

        var orderEntities = await context.Orders.AsNoTracking()
            .Where(o => o.UserId == id)
            .Include(o => o.OrderDetails).ThenInclude(d => d.Variant).ThenInclude(v => v.Product)
            .OrderByDescending(o => o.OrderDate)
            .Skip((orderPage - 1) * orderPageSize)
            .Take(orderPageSize)
            .ToListAsync();

        var orderRows = orderEntities.Select(o =>
        {
            var names = o.OrderDetails
                .Select(d =>
                    !string.IsNullOrEmpty(d.ProductName)
                        ? d.ProductName
                        : d.Variant?.Product?.Name ?? "—")
                .Distinct()
                .Take(4)
                .ToList();
            var summary = names.Count == 0
                ? "—"
                : string.Join(", ", names) + (o.OrderDetails.Count > 4 ? ", …" : "");

            return new AdminCustomerOrderRowVm
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode,
                OrderDate = o.OrderDate,
                ProductSummary = summary,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus
            };
        }).ToList();

        var reviewEntities = await context.Reviews.AsNoTracking()
            .Include(r => r.Product)
            .Where(r => r.UserId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        var reviews = reviewEntities
            .Select(r => new AdminCustomerReviewRowVm
            {
                ReviewId = r.ReviewId,
                ProductName = r.Product.Name,
                Rating = r.Rating,
                Content = r.Content ?? r.Comment,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            })
            .ToList();

        var vm = new AdminCustomerDetailVm
        {
            User = user,
            Addresses = addresses,
            Orders = orderRows,
            OrdersPage = orderPage,
            OrdersTotalPages = ordersTotalPages,
            OrdersTotalCount = totalOrders,
            Reviews = reviews,
            WishlistInDatabase = false,
            OrdersSummary = new AdminCustomerOrdersSummaryVm
            {
                TotalOrders = totalOrders,
                TotalPaidSpend = totalPaidSpend,
                CancelledOrders = cancelledOrders,
                CompletionRatePercent = completionRate
            },
            Sidebar = new AdminCustomerSidebarStatsVm
            {
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                CompletionRatePercent = completionRate,
                TotalPaidSpend = totalPaidSpend,
                AverageOrderValue = avgOrder,
                ReviewedProductCount = reviewedProductCount,
                Tier = CustomerTierHelper.GetTier(tierSpend)
            }
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        int id,
        bool activate,
        string? q,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        string sort = "created_desc",
        int page = 1)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.UserId == id && x.RoleId == 2);
        if (user != null)
        {
            user.IsActive = activate;
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { q, status, fromDate, toDate, sort, page });
    }
}
