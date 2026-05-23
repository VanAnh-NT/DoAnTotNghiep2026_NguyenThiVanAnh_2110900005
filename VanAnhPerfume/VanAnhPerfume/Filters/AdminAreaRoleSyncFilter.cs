using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;

namespace VanAnhPerfume.Filters;

/// <summary>
/// Với khu vực Admin: nếu cookie vẫn ghi Admin nhưng DB đã hạ quyền, đăng xuất để tránh truy cập trái phép.
/// </summary>
public sealed class AdminAreaRoleSyncFilter(VanAnhPerfumeContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!string.Equals(
                context.RouteData.Values["area"]?.ToString(),
                "Admin",
                StringComparison.OrdinalIgnoreCase))
            return;

        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
            return;

        if (!principal.IsInRole("Admin"))
            return;

        var userIdStr = principal.FindFirst("UserId")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return;

        var roleName = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role.RoleName)
            .FirstOrDefaultAsync();

        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
            return;

        await context.HttpContext.SignOutAsync("CookieAuth");
        context.Result = new RedirectToActionResult("Login", "Account", null);
    }
}
