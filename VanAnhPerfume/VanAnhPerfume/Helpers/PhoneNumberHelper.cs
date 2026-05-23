namespace VanAnhPerfume.Helpers;

/// <summary>Chuẩn hóa số điện thoại VN (chữ số, dạng 0xxxxxxxxx) để đăng nhập và lưu DB.</summary>
public static class PhoneNumberHelper
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digits = new string(raw.Trim().Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return string.Empty;
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length >= 10)
            digits = "0" + digits[2..];
        return digits;
    }

    /// <summary>Số VN thường 10–11 chữ số, bắt đầu bằng 0.</summary>
    public static bool IsValidLength(string normalized) =>
        normalized.Length is >= 10 and <= 11 && normalized.StartsWith('0');
}
