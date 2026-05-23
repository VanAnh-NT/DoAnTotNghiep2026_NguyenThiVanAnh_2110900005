using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using VanAnhPerfume.Models.Options;

namespace VanAnhPerfume.Services;

public interface IVnpayService
{
    string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, HttpContext httpContext);
    bool TryValidateResponse(IQueryCollection query, out Dictionary<string, string> data, out string responseCode);
}

public class VnpayService(IOptions<VnpayOptions> options) : IVnpayService
{
    private readonly VnpayOptions _options = options.Value;

    public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, HttpContext httpContext)
    {
        var txnRef = orderId.ToString(CultureInfo.InvariantCulture);
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");

        var payload = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = txnRef
        };

        // Khớp VNPAY mẫu (VnPayLibrary.CreateRequestUrl): WebUtility.UrlEncode — không dùng Uri.EscapeDataString
        var signData = BuildSignedQuery(payload);
        var secureHash = HmacSha512(_options.HashSecret, signData);

        var baseUrl = _options.PaymentUrl.Contains('?', StringComparison.Ordinal)
            ? _options.PaymentUrl + "&"
            : _options.PaymentUrl + "?";
        return baseUrl + signData + "&vnp_SecureHash=" + secureHash;
    }

    public bool TryValidateResponse(IQueryCollection query, out Dictionary<string, string> data, out string responseCode)
    {
        data = query
            .Where(x => x.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        responseCode = data.GetValueOrDefault("vnp_ResponseCode", string.Empty);

        if (!data.TryGetValue("vnp_SecureHash", out var hash))
        {
            return false;
        }

        var forSign = data
            .Where(x => x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType"))
            .Where(x => !string.IsNullOrEmpty(x.Value))
            .ToDictionary(x => x.Key, x => x.Value);

        var sorted = new SortedDictionary<string, string>(forSign, StringComparer.Ordinal);
        var raw = BuildSignedQuery(sorted);
        var expectedHash = HmacSha512(_options.HashSecret, raw);
        return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Giống VnPayLibrary: UrlEncode(key)=UrlEncode(value)&amp;... (bỏ &amp; cuối).
    /// </summary>
    private static string BuildSignedQuery(SortedDictionary<string, string> payload)
    {
        var data = new StringBuilder();
        foreach (var kv in payload)
        {
            if (string.IsNullOrEmpty(kv.Value))
            {
                continue;
            }

            data.Append(WebUtility.UrlEncode(kv.Key))
                .Append('=')
                .Append(WebUtility.UrlEncode(kv.Value))
                .Append('&');
        }

        if (data.Length > 0)
        {
            data.Length -= 1;
        }

        return data.ToString();
    }

    private static string HmacSha512(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        var hashValue = hmac.ComputeHash(inputBytes);
        var hash = new StringBuilder(hashValue.Length * 2);
        foreach (var b in hashValue)
        {
            hash.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return hash.ToString();
    }
}
