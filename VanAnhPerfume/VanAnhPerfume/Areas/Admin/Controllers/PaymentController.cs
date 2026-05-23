using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PaymentController(VanAnhPerfumeContext context) : Controller
{
    public async Task<IActionResult> Index(string? paymentStatus, string? paymentMethod, DateTime? fromDate, DateTime? toDate, string? q, string? sort, int page = 1)
    {
        var query = context.Payments.AsNoTracking()
            .Include(x => x.Order).ThenInclude(o => o.User)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(x => x.Status == paymentStatus);
        if (!string.IsNullOrWhiteSpace(paymentMethod)) query = query.Where(x => x.PaymentMethod == paymentMethod);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAt < toDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x =>
                (x.Order.OrderCode ?? "").Contains(term) ||
                (x.TransactionId ?? "").Contains(term) ||
                x.Order.FullName.Contains(term));
        }
        query = sort == "amount" ? query.OrderByDescending(x => x.Amount) : query.OrderByDescending(x => x.CreatedAt);

        const int pageSize = 20;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var payments = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Stats = await context.Payments.AsNoTracking()
            .GroupBy(x => x.Status ?? "Pending")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
        ViewBag.PaymentStatus = paymentStatus; ViewBag.PaymentMethod = paymentMethod; ViewBag.FromDate = fromDate; ViewBag.ToDate = toDate; ViewBag.Query = q; ViewBag.Sort = sort;
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(payments);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int paymentId, string status, string note)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var payment = await context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.PaymentId == paymentId);
            if (payment == null) return RedirectToAction(nameof(Index));

            var oldStatus = payment.Status ?? "Pending";
            var newStatus = status.Trim();
            if (oldStatus == "Refunded") return RedirectToAction(nameof(Index));
            if (oldStatus == "Paid" && (newStatus == "Pending" || newStatus == "Failed")) return RedirectToAction(nameof(Index));
            if (string.IsNullOrWhiteSpace(note)) return RedirectToAction(nameof(Index));

            await using var tx = await context.Database.BeginTransactionAsync();

            payment.Status = newStatus;
            payment.UpdatedAt = DateTime.UtcNow;
            if (newStatus == "Paid")
            {
                payment.PaidAt = DateTime.UtcNow;
                payment.RefundedAt = null;
                payment.RefundAmount = null;
                if (payment.Order.Status == "Pending")
                {
                    var oldOrderStatus = payment.Order.Status;
                    payment.Order.Status = "Processing";
                    payment.Order.UpdatedAt = DateTime.UtcNow;
                    context.OrderStatusLogs.Add(new OrderStatusLog
                    {
                        OrderId = payment.OrderId,
                        OldStatus = oldOrderStatus,
                        NewStatus = payment.Order.Status,
                        OldPaymentStatus = oldStatus,
                        NewPaymentStatus = newStatus,
                        Note = "Payment manually set to paid",
                        ChangedBy = User.Identity?.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                payment.Order.PaymentStatus = "Paid";
            }
            else if (newStatus == "Refunded")
            {
                payment.RefundedAt = DateTime.UtcNow;
                payment.RefundAmount = payment.Amount;
                payment.RefundNote = note;
                payment.Order.PaymentStatus = "Refunded";
                if (payment.Order.Status != "Cancelled")
                {
                    var oldOrderStatus = payment.Order.Status;
                    payment.Order.Status = "Cancelled";
                    payment.Order.UpdatedAt = DateTime.UtcNow;
                    context.OrderStatusLogs.Add(new OrderStatusLog
                    {
                        OrderId = payment.OrderId,
                        OldStatus = oldOrderStatus,
                        NewStatus = "Cancelled",
                        OldPaymentStatus = oldStatus,
                        NewPaymentStatus = "Refunded",
                        Note = "Auto cancelled by payment refund",
                        ChangedBy = User.Identity?.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                    await RestoreReservedInventoryAsync(payment.OrderId, $"Refunded payment #{payment.PaymentId}");
                }
            }
            else if (newStatus == "Failed")
            {
                payment.Order.PaymentStatus = "Failed";
            }

            context.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Note = note,
                Source = "Admin",
                LogDate = DateTime.UtcNow,
                Message = $"ADMIN:{oldStatus}->{newStatus}"
            });

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            return RedirectToAction(nameof(Index));
        });
    }

    [HttpGet]
    public async Task<IActionResult> Logs(int paymentId)
    {
        var payment = await context.Payments.AsNoTracking().Include(x => x.Order).FirstOrDefaultAsync(x => x.PaymentId == paymentId);
        if (payment == null) return Content("Không tìm thấy payment.");
        var logs = await context.PaymentLogs.AsNoTracking().Where(x => x.PaymentId == paymentId).OrderByDescending(x => x.LogDate).ToListAsync();
        var orderLogs = await context.OrderStatusLogs.AsNoTracking().Where(x => x.OrderId == payment.OrderId).OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync();
        ViewBag.Payment = payment;
        ViewBag.OrderLogs = orderLogs;
        return PartialView("_PaymentLogsPartial", logs);
    }

    private async Task RestoreReservedInventoryAsync(int orderId, string note)
    {
        var lines = await context.OrderDetails.Include(x => x.Variant).ThenInclude(v => v.Inventory).Where(x => x.OrderId == orderId).ToListAsync();
        foreach (var line in lines)
        {
            var inv = line.Variant.Inventory;
            if (inv == null) continue;
            inv.QuantityReserved = Math.Max(0, inv.QuantityReserved - line.Quantity);
            inv.UpdatedAt = DateTime.UtcNow;
            context.InventoryLogs.Add(new InventoryLog
            {
                VariantId = line.VariantId,
                ActionType = "Cancel",
                ChangeType = "Cancel",
                QuantityDelta = line.Quantity,
                QuantityChange = line.Quantity,
                StockAfter = inv.QuantityAvailable,
                QuantityAfter = inv.QuantityAvailable,
                Note = note,
                CreatedBy = User.Identity?.Name,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
