using System.Net;
using System.Net.Mail;

namespace VanAnhPerfume.Services;

public interface IEmailService
{
    /// <returns>true nếu đã gửi qua SMTP; false nếu thiếu cấu hình hoặc gửi lỗi.</returns>
    Task<bool> SendAsync(string to, string subject, string bodyHtml);
}

public class EmailService(IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    public async Task<bool> SendAsync(string to, string subject, string bodyHtml)
    {
        // Prefer appsettings Smtp:*; fallback to EMAIL_* env vars for local/dev.
        var host = configuration["Smtp:Host"];
        var portRaw = configuration["Smtp:Port"];
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        var from = configuration["Smtp:From"] ?? username;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
        {
            host = Environment.GetEnvironmentVariable("EMAIL_HOST") ?? configuration["EMAIL_HOST"];
            portRaw = Environment.GetEnvironmentVariable("EMAIL_PORT") ?? configuration["EMAIL_PORT"];
            username = Environment.GetEnvironmentVariable("EMAIL_USER") ?? configuration["EMAIL_USER"];
            password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD") ?? configuration["EMAIL_PASSWORD"];
            from = (Environment.GetEnvironmentVariable("EMAIL_FROM") ?? configuration["EMAIL_FROM"]) ?? username;
        }

        var port = int.TryParse(portRaw, out var p) ? p : 587;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("SMTP not configured. Email to {Email}: {Body}", to, bodyHtml);
            return false;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };
            using var message = new MailMessage(from, to, subject, bodyHtml) { IsBodyHtml = true };
            await client.SendMailAsync(message).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP send failed to {Email}", to);
            return false;
        }
    }
}
