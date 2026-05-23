using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AccountsController(VanAnhPerfumeContext context) : Controller
{
    private int CurrentUserId => int.TryParse(User.FindFirstValue("UserId"), out var id) ? id : 0;

    public async Task<IActionResult> Index(string? q, string role = "all", int page = 1)
    {
        var adminRoleId = await context.Roles
            .Where(r => r.RoleName == "Admin")
            .Select(r => r.RoleId)
            .FirstAsync();
        var customerRoleId = await context.Roles
            .Where(r => r.RoleName == "Customer")
            .Select(r => r.RoleId)
            .FirstAsync();

        var baseUsers = context.Users.AsNoTracking().Include(u => u.Role);

        var stats = new AdminAccountsStatsVm
        {
            Total = await baseUsers.CountAsync(),
            AdminCount = await baseUsers.CountAsync(u => u.RoleId == adminRoleId),
            CustomerCount = await baseUsers.CountAsync(u => u.RoleId == customerRoleId)
        };

        var query = baseUsers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.RoleId == adminRoleId);
            else if (role.Equals("customer", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.RoleId == customerRoleId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(u =>
                u.FullName.Contains(t) ||
                u.Email.Contains(t) ||
                (u.Phone != null && u.Phone.Contains(t)));
        }

        var rowQuery = query
            .OrderByDescending(u => u.RoleId == adminRoleId)
            .ThenByDescending(u => u.CreatedAt)
            .Select(u => new AdminAccountRowVm
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                RoleName = u.Role.RoleName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            });

        const int pageSize = 20;
        var totalItems = await rowQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rows = await rowQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var vm = new AdminAccountsIndexVm
        {
            Stats = stats,
            Rows = rows,
            Page = page,
            TotalPages = totalPages,
            Q = q,
            RoleFilter = string.IsNullOrWhiteSpace(role) ? "all" : role.ToLowerInvariant(),
            CurrentOperatorUserId = CurrentUserId
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToAdmin(int id, string? q, string role = "all", int page = 1)
    {
        var adminRoleId = await context.Roles.Where(r => r.RoleName == "Admin").Select(r => r.RoleId).FirstAsync();
        var customerRoleId = await context.Roles.Where(r => r.RoleName == "Customer").Select(r => r.RoleId).FirstAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        if (user.RoleId != customerRoleId)
        {
            TempData["Error"] = "Chỉ có thể nâng quyền tài khoản đang là khách hàng.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        user.RoleId = adminRoleId;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        TempData["Success"] = $"Đã cấp quyền quản trị cho {user.Email}.";
        return RedirectToAction(nameof(Index), new { q, role, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoteToCustomer(int id, string? q, string role = "all", int page = 1)
    {
        var adminRoleId = await context.Roles.Where(r => r.RoleName == "Admin").Select(r => r.RoleId).FirstAsync();
        var customerRoleId = await context.Roles.Where(r => r.RoleName == "Customer").Select(r => r.RoleId).FirstAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy tài khoản.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        if (user.RoleId != adminRoleId)
        {
            TempData["Error"] = "Chỉ có thể hạ quyền tài khoản đang là quản trị.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        if (user.UserId == CurrentUserId)
        {
            TempData["Error"] = "Bạn không thể hạ quyền chính mình.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        var adminCount = await context.Users.CountAsync(u => u.RoleId == adminRoleId);
        if (adminCount <= 1)
        {
            TempData["Error"] = "Phải có ít nhất một tài khoản quản trị. Không thể hạ quyền admin cuối cùng.";
            return RedirectToAction(nameof(Index), new { q, role, page });
        }

        user.RoleId = customerRoleId;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        TempData["Success"] = $"Đã hạ quyền {user.Email} xuống khách hàng.";
        return RedirectToAction(nameof(Index), new { q, role, page });
    }
}
