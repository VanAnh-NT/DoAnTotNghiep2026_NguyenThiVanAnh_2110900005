using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Services;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class OrderController(VanAnhPerfumeContext context, IEmailService emailService) : Controller
{
    private static readonly string[] OrderedStatuses = ["Pending", "Processing", "Shipped", "Delivered", "Completed", "Cancelled"];

    public async Task<IActionResult> Index(string? orderStatus, string? paymentStatus, string? paymentMethod, string? q, DateTime? fromDate, DateTime? toDate, string? sort, int page = 1)
    {
        var query = context.Orders.AsNoTracking().Include(x => x.Payments).Include(x => x.OrderDetails).AsQueryable();
        if (!string.IsNullOrWhiteSpace(orderStatus)) query = query.Where(x => x.Status == orderStatus);
        if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(x => x.PaymentStatus == paymentStatus);
        if (!string.IsNullOrWhiteSpace(paymentMethod)) query = query.Where(x => x.Payments.Any(p => p.PaymentMethod == paymentMethod));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => (x.OrderCode ?? "").Contains(term) || x.FullName.Contains(term) || (x.CustomerEmail ?? "").Contains(term) || x.Phone.Contains(term));
        }
        if (fromDate.HasValue) query = query.Where(x => x.OrderDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.OrderDate < toDate.Value.Date.AddDays(1));

        query = sort switch
        {
            "total" => query.OrderByDescending(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.OrderDate)
        };

        const int pageSize = 20;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var orders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var stats = await context.Orders.AsNoTracking()
            .GroupBy(x => x.Status ?? "Pending")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        ViewBag.Stats = stats;
        ViewBag.OrderStatus = orderStatus; ViewBag.PaymentStatus = paymentStatus; ViewBag.PaymentMethod = paymentMethod;
        ViewBag.Query = q; ViewBag.FromDate = fromDate; ViewBag.ToDate = toDate; ViewBag.Sort = sort;
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> BulkConfirm([FromForm] List<int> ids)
    {
        if (!ids.Any()) return RedirectToAction(nameof(Index));
        var orders = await context.Orders.Where(x => ids.Contains(x.OrderId) && x.Status == "Pending").ToListAsync();
        foreach (var o in orders)
        {
            o.Status = "Processing";
            o.UpdatedAt = DateTime.UtcNow;
            context.OrderStatusLogs.Add(new OrderStatusLog
            {
                OrderId = o.OrderId,
                OldStatus = "Pending",
                NewStatus = "Processing",
                Note = "Bulk confirm",
                ChangedBy = User.Identity?.Name,
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? orderStatus, string? paymentStatus, string? paymentMethod, string? q, DateTime? fromDate, DateTime? toDate, string? sort)
    {
        var query = context.Orders.AsNoTracking().Include(x => x.User).Include(x => x.Payments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(orderStatus)) query = query.Where(x => x.Status == orderStatus);
        if (!string.IsNullOrWhiteSpace(paymentStatus)) query = query.Where(x => x.PaymentStatus == paymentStatus);
        if (!string.IsNullOrWhiteSpace(paymentMethod)) query = query.Where(x => x.Payments.Any(p => p.PaymentMethod == paymentMethod));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => (x.OrderCode ?? "").Contains(term) || x.FullName.Contains(term) || (x.CustomerEmail ?? "").Contains(term) || x.Phone.Contains(term));
        }
        if (fromDate.HasValue) query = query.Where(x => x.OrderDate >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.OrderDate < toDate.Value.Date.AddDays(1));
        query = sort == "total" ? query.OrderByDescending(x => x.TotalAmount) : query.OrderByDescending(x => x.OrderDate);
        var orders = await query.ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("OrderCode,CustomerName,Email,Phone,OrderStatus,PaymentStatus,PaymentMethod,TotalAmount,CreatedAt");
        foreach (var o in orders)
        {
            var paymentMethodText = o.Payments.OrderByDescending(x => x.PaymentId).Select(x => x.PaymentMethod).FirstOrDefault() ?? "COD";
            sb.AppendLine($"{o.OrderCode},\"{Escape(o.FullName)}\",\"{Escape(o.CustomerEmail ?? o.User?.Email)}\",\"{Escape(o.Phone)}\",\"{o.Status}\",\"{o.PaymentStatus}\",\"{paymentMethodText}\",{o.TotalAmount},{o.OrderDate:yyyy-MM-dd HH:mm:ss}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"orders_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await context.Orders
            .Include(o => o.OrderDetails).ThenInclude(d => d.Variant).ThenInclude(v => v.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(m => m.OrderId == id);
        if (order == null) return NotFound();
        ViewBag.Timeline = await context.OrderStatusLogs.Where(x => x.OrderId == id).OrderByDescending(x => x.CreatedAt).ToListAsync();
        ViewBag.NextStatuses = GetAllowedNextStatuses(order.Status ?? "Pending");
        ViewBag.Payment = order.Payments.OrderByDescending(x => x.PaymentId).FirstOrDefault();
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int orderId, string newStatus, string? adminNote)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync<IActionResult?>(async () =>
        {
            var order = await context.Orders.Include(x => x.Payments).FirstOrDefaultAsync(x => x.OrderId == orderId);
            if (order == null) return null;
            var currentStatus = order.Status ?? "Pending";
            var allowed = GetAllowedNextStatuses(currentStatus);
            if (!allowed.Contains(newStatus))
            {
                TempData["Error"] = "Chuyển trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var oldPaymentStatus = order.PaymentStatus;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(adminNote)) order.AdminNote = adminNote.Trim();

            var payment = order.Payments.OrderByDescending(x => x.PaymentId).FirstOrDefault();
            if (payment != null)
            {
                var oldPayment = payment.Status ?? order.PaymentStatus;
                if (newStatus == "Delivered" && string.Equals(payment.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = "Paid";
                    payment.PaidAt = DateTime.UtcNow;
                    order.PaymentStatus = "Paid";
                    context.PaymentLogs.Add(new PaymentLog
                    {
                        PaymentId = payment.PaymentId,
                        OrderId = orderId,
                        OldStatus = oldPayment,
                        NewStatus = "Paid",
                        Source = "System",
                        Note = "COD order delivered",
                        LogDate = DateTime.UtcNow
                    });
                }
                else if (newStatus == "Cancelled")
                {
                    if (payment.Status == "Paid")
                    {
                        payment.Status = "Refunded";
                        payment.RefundedAt = DateTime.UtcNow;
                        payment.RefundAmount = payment.Amount;
                        order.PaymentStatus = "Refunded";
                        context.PaymentLogs.Add(new PaymentLog
                        {
                            PaymentId = payment.PaymentId,
                            OrderId = orderId,
                            OldStatus = oldPayment,
                            NewStatus = "Refunded",
                            Source = "System",
                            Note = "Order cancelled after paid",
                            LogDate = DateTime.UtcNow
                        });
                    }
                    else if (payment.Status == "Pending")
                    {
                        payment.Status = "Failed";
                        order.PaymentStatus = "Failed";
                        context.PaymentLogs.Add(new PaymentLog
                        {
                            PaymentId = payment.PaymentId,
                            OrderId = orderId,
                            OldStatus = oldPayment,
                            NewStatus = "Failed",
                            Source = "System",
                            Note = "Order cancelled while pending payment",
                            LogDate = DateTime.UtcNow
                        });
                    }
                }
            }

            if (newStatus == "Completed" && order.PaymentStatus != "Paid")
            {
                TempData["Error"] = "Không thể Completed khi thanh toán chưa Paid.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (newStatus == "Delivered")
            {
                var lines = await context.OrderDetails.Include(x => x.Variant).ThenInclude(v => v.Inventory).Where(x => x.OrderId == orderId).ToListAsync();
                foreach (var line in lines)
                {
                    var inv = line.Variant.Inventory ?? new Inventory
                    {
                        ProductVariantId = line.VariantId,
                        QuantityAvailable = Math.Max(0, line.Variant.Stock),
                        QuantityReserved = 0,
                        QuantitySold = 0,
                        LowStockThreshold = 5,
                        UpdatedAt = DateTime.UtcNow
                    };
                    if (line.Variant.Inventory == null) context.Inventories.Add(inv);
                    inv.QuantityReserved = Math.Max(0, inv.QuantityReserved - line.Quantity);
                    inv.QuantityAvailable = Math.Max(0, inv.QuantityAvailable - line.Quantity);
                    inv.QuantitySold += line.Quantity;
                    inv.UpdatedAt = DateTime.UtcNow;
                    line.Variant.Stock = inv.QuantityAvailable;
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        VariantId = line.VariantId,
                        ActionType = "Sale",
                        ChangeType = "Sale",
                        QuantityDelta = -line.Quantity,
                        QuantityChange = -line.Quantity,
                        StockAfter = inv.QuantityAvailable,
                        QuantityAfter = inv.QuantityAvailable,
                        Note = $"Order {order.OrderCode} delivered",
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else if (newStatus == "Cancelled")
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
                        Note = $"Order {order.OrderCode} cancelled",
                        CreatedBy = User.Identity?.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            context.OrderStatusLogs.Add(new OrderStatusLog
            {
                OrderId = orderId,
                OldStatus = currentStatus,
                NewStatus = newStatus,
                OldPaymentStatus = oldPaymentStatus,
                NewPaymentStatus = order.PaymentStatus,
                Note = adminNote,
                ChangedBy = User.Identity?.Name,
                CreatedAt = DateTime.UtcNow
            });

            await using var tx = await context.Database.BeginTransactionAsync();
            await context.SaveChangesAsync();
            await tx.CommitAsync();

            await SendStatusEmailAsync(orderId, newStatus);
            return RedirectToAction(nameof(Details), new { id = orderId });
        });

        return result ?? NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> SaveAdminNote(int orderId, string adminNote)
    {
        var order = await context.Orders.FirstOrDefaultAsync(x => x.OrderId == orderId);
        if (order == null) return NotFound();
        order.AdminNote = adminNote;
        order.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(int id)
    {
        var order = await context.Orders
            .Include(x => x.OrderDetails).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == id);
        if (order == null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine($"INVOICE {order.OrderCode ?? $"#{order.OrderId}"}");
        sb.AppendLine($"Customer: {order.FullName}");
        sb.AppendLine($"Phone: {order.Phone}");
        sb.AppendLine($"Address: {order.Address}");
        sb.AppendLine($"Date: {order.OrderDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("------------------------------------------------");
        foreach (var d in order.OrderDetails)
        {
            var pName = d.ProductName ?? d.Variant.Product.Name;
            var vName = d.VariantName ?? d.Variant.Size;
            sb.AppendLine($"{pName} - {vName} x{d.Quantity} = {(d.Price * d.Quantity):N0} VND");
        }
        sb.AppendLine("------------------------------------------------");
        sb.AppendLine($"TOTAL: {order.TotalAmount:N0} VND");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "application/pdf", $"invoice_{order.OrderId}.pdf");
    }

    private async Task SendStatusEmailAsync(int orderId, string newStatus)
    {
        var order = await context.Orders.Include(x => x.User).FirstOrDefaultAsync(x => x.OrderId == orderId);
        if (order == null) return;
        var to = order.CustomerEmail ?? order.User?.Email;
        if (string.IsNullOrWhiteSpace(to)) return;
        var trackingUrl = Url.Action("OrderTracking", "Account", new { id = order.OrderId }, Request.Scheme) ?? $"/Account/OrderTracking/{order.OrderId}";
        var subject = $"[{order.OrderCode}] Cập nhật đơn hàng: {newStatus}";
        var body = $"""
<div style='font-family:Inter,Arial,sans-serif'>
  <h3>Vân Anh Perfume</h3>
  <p>Đơn hàng <strong>{order.OrderCode ?? $"#{order.OrderId}"}</strong> đã chuyển sang trạng thái <strong>{newStatus}</strong>.</p>
  <p>Khách hàng: {order.FullName} - {order.Phone}</p>
  <p>Tổng tiền: {order.TotalAmount:N0} ₫</p>
  <p><a href='{trackingUrl}'>Theo dõi đơn hàng</a></p>
</div>
""";
        await emailService.SendAsync(to, subject, body);
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");

    private static List<string> GetAllowedNextStatuses(string current)
    {
        return current switch
        {
            "Pending" => ["Processing", "Cancelled"],
            "Processing" => ["Shipped", "Cancelled"],
            "Shipped" => ["Delivered", "Cancelled"],
            "Delivered" => ["Completed"],
            _ => []
        };
    }
}
