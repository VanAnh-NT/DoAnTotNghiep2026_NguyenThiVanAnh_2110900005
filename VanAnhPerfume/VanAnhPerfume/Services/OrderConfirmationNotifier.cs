using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;

namespace VanAnhPerfume.Services;

public interface IOrderConfirmationNotifier
{
    /// <summary>Gửi sau khi tạo đơn COD thành công.</summary>
    Task NotifyCodOrderPlacedAsync(int orderId, HttpRequest? request);

    /// <summary>Gửi sau khi VNPay báo thanh toán thành công (Return hoặc IPN).</summary>
    Task NotifyVnpayPaymentSucceededAsync(int orderId, HttpRequest? request);
}

public class OrderConfirmationNotifier(
    VanAnhPerfumeContext context,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<OrderConfirmationNotifier> logger) : IOrderConfirmationNotifier
{
    public Task NotifyCodOrderPlacedAsync(int orderId, HttpRequest? request)
        => SendConfirmationAsync(orderId, request, isVnpayPaid: false);

    public Task NotifyVnpayPaymentSucceededAsync(int orderId, HttpRequest? request)
        => SendConfirmationAsync(orderId, request, isVnpayPaid: true);

    private async Task SendConfirmationAsync(int orderId, HttpRequest? request, bool isVnpayPaid)
    {
        var to = await ResolveRecipientEmailAsync(orderId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogInformation("Order {OrderId}: bỏ qua email xác nhận — không có địa chỉ email.", orderId);
            return;
        }

        var now = DateTime.UtcNow;
        var rows = await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Orders SET ConfirmationEmailSentAt = {now} WHERE OrderId = {orderId} AND ConfirmationEmailSentAt IS NULL")
            .ConfigureAwait(false);
        if (rows != 1)
            return;

        var order = await context.Orders
            .AsNoTracking()
            .Include(o => o.OrderDetails)
            .ThenInclude(d => d.Variant)
            .ThenInclude(v => v.Product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == orderId)
            .ConfigureAwait(false);

        if (order == null)
        {
            logger.LogWarning("Order {OrderId}: không tải được sau khi đánh dấu email — hoàn tác cờ.", orderId);
            await ClearConfirmationEmailFlagAsync(orderId).ConfigureAwait(false);
            return;
        }

        var baseUrl = GetPublicBaseUrl(request);
        var orderLabel = WebUtility.HtmlEncode(order.OrderCode ?? $"#{order.OrderId}");
        var subject = isVnpayPaid
            ? $"[{order.OrderCode ?? $"#{order.OrderId}"}] Thanh toán thành công"
            : $"[{order.OrderCode ?? $"#{order.OrderId}"}] Đã tiếp nhận đơn hàng";

        var paymentLabel = order.Payments.OrderByDescending(p => p.PaymentId).FirstOrDefault()?.PaymentMethod ?? "COD";
        if (isVnpayPaid)
            paymentLabel = "VNPay";

        var trackingUrl = $"{baseUrl}/Account/OrderTracking/{order.OrderId}";
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Inter,Arial,sans-serif;font-size:14px;color:#1a1a1a;line-height:1.5;\">");
        sb.Append("<h2 style=\"margin:0 0 12px;\">Vân Anh Perfume</h2>");
        if (isVnpayPaid)
        {
            sb.Append($"<p>Xin chào <strong>{WebUtility.HtmlEncode(order.FullName)}</strong>,</p>");
            sb.Append($"<p>Đơn hàng <strong>{orderLabel}</strong> đã <strong>thanh toán thành công</strong> qua VNPay.</p>");
        }
        else
        {
            sb.Append($"<p>Xin chào <strong>{WebUtility.HtmlEncode(order.FullName)}</strong>,</p>");
            sb.Append($"<p>Cảm ơn bạn đã đặt hàng. Đơn <strong>{orderLabel}</strong> đã được <strong>tiếp nhận</strong> (thanh toán khi nhận hàng — COD).</p>");
        }

        sb.Append("<ul style=\"padding-left:18px;margin:12px 0;\">");
        sb.Append($"<li>Mã đơn: <strong>{orderLabel}</strong></li>");
        sb.Append($"<li>Tổng thanh toán: <strong>{order.TotalAmount:N0} ₫</strong></li>");
        sb.Append($"<li>Phương thức: <strong>{WebUtility.HtmlEncode(paymentLabel)}</strong></li>");
        sb.Append("</ul>");

        sb.Append("<p style=\"margin:16px 0 8px;\"><strong>Chi tiết sản phẩm</strong></p>");
        sb.Append("<table style=\"border-collapse:collapse;width:100%;max-width:560px;border:1px solid #ddd;\">");
        sb.Append("<thead><tr style=\"background:#f5f5f5;\"><th style=\"text-align:left;padding:8px;border:1px solid #ddd;\">Sản phẩm</th><th style=\"text-align:right;padding:8px;border:1px solid #ddd;\">SL</th><th style=\"text-align:right;padding:8px;border:1px solid #ddd;\">Đơn giá</th></tr></thead><tbody>");
        foreach (var line in order.OrderDetails)
        {
            var pName = WebUtility.HtmlEncode(line.ProductName ?? line.Variant?.Product?.Name ?? "Sản phẩm");
            var vName = WebUtility.HtmlEncode(line.VariantName ?? line.Variant?.Size ?? "");
            sb.Append("<tr>");
            sb.Append($"<td style=\"padding:8px;border:1px solid #ddd;\">{pName}" + (string.IsNullOrEmpty(vName) ? "" : $" <span style=\"color:#666;\">({vName})</span>") + "</td>");
            sb.Append($"<td style=\"text-align:right;padding:8px;border:1px solid #ddd;\">{line.Quantity}</td>");
            sb.Append($"<td style=\"text-align:right;padding:8px;border:1px solid #ddd;\">{line.Price:N0} ₫</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");

        sb.Append($"<p style=\"margin:16px 0 8px;\"><a href=\"{WebUtility.HtmlEncode(trackingUrl)}\" style=\"color:#1a1a1a;font-weight:600;\">Theo dõi đơn hàng</a></p>");

        sb.Append("<p style=\"margin:16px 0 8px;\"><strong>Đánh giá sản phẩm</strong></p>");
        sb.Append("<p style=\"color:#555;font-size:13px;margin:0 0 8px;\">Sau khi shop xác nhận đơn hoàn thành, bạn có thể đăng nhập bằng tài khoản đã đặt hàng và đánh giá tại các link sau:</p>");
        sb.Append("<ul style=\"padding-left:18px;margin:0;\">");
        var seen = new HashSet<int>();
        foreach (var line in order.OrderDetails)
        {
            var pid = line.Variant?.ProductId ?? 0;
            if (pid == 0 || !seen.Add(pid)) continue;
            var title = WebUtility.HtmlEncode(line.Variant?.Product?.Name ?? line.ProductName ?? "Sản phẩm");
            var reviewUrl = $"{baseUrl}/Product/Detail/{pid}#reviews";
            sb.Append($"<li><a href=\"{WebUtility.HtmlEncode(reviewUrl)}\">{title}</a></li>");
        }

        sb.Append("</ul>");
        sb.Append("<p style=\"color:#888;font-size:12px;margin-top:16px;\">Đây là email tự động, vui lòng không trả lời trực tiếp.</p>");
        sb.Append("</div>");

        var sent = await emailService.SendAsync(to.Trim(), subject, sb.ToString()).ConfigureAwait(false);
        if (!sent)
        {
            await ClearConfirmationEmailFlagAsync(orderId).ConfigureAwait(false);
            logger.LogWarning(
                "Order {OrderId}: gửi email không thành công (SMTP lỗi hoặc chưa cấu hình). Đã đặt lại ConfirmationEmailSentAt để lần sau thử lại.",
                orderId);
        }
    }

    private Task<int> ClearConfirmationEmailFlagAsync(int orderId) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Orders SET ConfirmationEmailSentAt = NULL WHERE OrderId = {orderId}");

    private async Task<string?> ResolveRecipientEmailAsync(int orderId)
    {
        var row = await context.Orders
            .AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => new { o.CustomerEmail, o.UserId })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row == null) return null;
        if (!string.IsNullOrWhiteSpace(row.CustomerEmail))
            return row.CustomerEmail.Trim();
        if (row.UserId is { } uid)
        {
            return await context.Users.AsNoTracking()
                .Where(u => u.UserId == uid)
                .Select(u => u.Email)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        return null;
    }

    private string GetPublicBaseUrl(HttpRequest? request)
    {
        var configured = configuration["App:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
            return configured;
        if (request != null)
            return $"{request.Scheme}://{request.Host}";
        logger.LogWarning("App:PublicBaseUrl chưa cấu hình và không có HttpRequest — dùng https://localhost:3000 cho link email.");
        return "https://localhost:3000";
    }
}
