using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels;
using VanAnhPerfume.Services;
using VanAnhPerfume.Helpers;

namespace VanAnhPerfume.Controllers
{
    public class AccountController : Controller
    {
        private const string GUEST_ORDER_LIST_KEY = "VanAnhGuestOrderIds";
        private const string WishlistSessionKey = "VanAnhWishlist";
        private readonly VanAnhPerfumeContext _context;
        private readonly IEmailService _emailService;

        public AccountController(VanAnhPerfumeContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var phoneNorm = PhoneNumberHelper.Normalize(model.Phone);
            if (!PhoneNumberHelper.IsValidLength(phoneNorm))
            {
                ModelState.AddModelError(nameof(model.Phone), "Số điện thoại không hợp lệ (VD: 09xx xxx xxx).");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã tồn tại.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Phone == phoneNorm))
            {
                ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã được đăng ký.");
                return View(model);
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                Phone = phoneNorm,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = 2, // Customer
                CreatedAt = now,
                UpdatedAt = now,
                Status = true,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null) => View(new LoginVM { ReturnUrl = returnUrl });

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var phone = PhoneNumberHelper.Normalize(model.Phone);
            if (!PhoneNumberHelper.IsValidLength(phone))
            {
                ModelState.AddModelError(nameof(model.Phone), "Số điện thoại không hợp lệ (VD: 09xx xxx xxx).");
                return View(model);
            }

            var user = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Phone != null && u.Phone == phone);

            if (user == null)
            {
                user = await _context.Users.Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Phone != null &&
                        u.Phone.Replace(" ", "").Replace("-", "").Replace(".", "") == phone);
            }

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
            {
                ModelState.AddModelError("", "Số điện thoại hoặc mật khẩu không chính xác.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ hỗ trợ.");
                return View(model);
            }

            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var claims = new List<Claim> {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("UserId", user.UserId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "CookieAuth");
            var principal = new ClaimsPrincipal(identity);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe
            };
            await HttpContext.SignInAsync("CookieAuth", principal, authProperties);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            if (string.Equals(user.Role.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Main", new { area = "Admin" });
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordVM());

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email là bắt buộc.");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == model.Email && x.IsActive);
            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                _context.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserId = user.UserId,
                    Token = token,
                    ExpiryDate = DateTime.UtcNow.AddHours(2),
                    IsUsed = false
                });
                await _context.SaveChangesAsync();

                var resetLink = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme);
                await _emailService.SendAsync(
                    user.Email,
                    "VanAnhPerfume - Reset password",
                    $"<p>Nhấn link để đặt lại mật khẩu:</p><p><a href='{resetLink}'>{resetLink}</a></p>");
            }

            TempData["Success"] = "Nếu email tồn tại, link đặt lại mật khẩu đã được gửi.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            return View(new ResetPasswordVM { Token = token });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            if (string.IsNullOrWhiteSpace(model.Token))
            {
                ModelState.AddModelError("", "Token không hợp lệ.");
                return View(model);
            }
            if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới tối thiểu 6 ký tự.");
            }
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Xác nhận mật khẩu không khớp.");
            }
            if (!ModelState.IsValid) return View(model);

            var tokenEntity = await _context.PasswordResetTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == model.Token && !x.IsUsed && x.ExpiryDate > DateTime.UtcNow);
            if (tokenEntity == null)
            {
                ModelState.AddModelError("", "Token hết hạn hoặc không tồn tại.");
                return View(model);
            }

            tokenEntity.User.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            tokenEntity.IsUsed = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return RedirectToAction("Login");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile(string? tab = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction(nameof(Login));

            var dashboard = await BuildAccountDashboardAsync(userId.Value);
            ViewBag.ActiveTab = NormalizeAccountTab(tab);
            return View(dashboard);
        }

        /// <summary>Cập nhật họ tên / SĐT — nằm trong trang Profile (tab).</summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileVM model)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
            {
                var dashInvalid = await BuildAccountDashboardAsync(userId.Value);
                dashInvalid.Profile = model;
                ViewBag.ActiveTab = "profile";
                return View("Profile", dashInvalid);
            }

            var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId.Value);
            if (user == null) return RedirectToAction(nameof(Login));

            var phoneNorm = PhoneNumberHelper.Normalize(model.Phone);
            if (!PhoneNumberHelper.IsValidLength(phoneNorm))
            {
                ModelState.AddModelError(nameof(model.Phone), "Số điện thoại không hợp lệ (VD: 09xx xxx xxx).");
                model.Email = user.Email;
                var dashErr = await BuildAccountDashboardAsync(userId.Value);
                dashErr.Profile = model;
                ViewBag.ActiveTab = "profile";
                return View("Profile", dashErr);
            }

            if (await _context.Users.AnyAsync(u => u.Phone == phoneNorm && u.UserId != userId.Value))
            {
                ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã được dùng cho tài khoản khác.");
                model.Email = user.Email;
                var dashErr = await BuildAccountDashboardAsync(userId.Value);
                dashErr.Profile = model;
                ViewBag.ActiveTab = "profile";
                return View("Profile", dashErr);
            }

            user.FullName = model.FullName;
            user.Phone = phoneNorm;
            await _context.SaveChangesAsync();

            TempData["ProfileMessage"] = "Cập nhật tài khoản thành công.";
            return RedirectToAction(nameof(Profile), new { tab = "profile" });
        }

        private static string NormalizeAccountTab(string? tab)
        {
            var t = (tab ?? "profile").Trim().ToLowerInvariant();
            return t switch
            {
                "orders" => "orders",
                "wishlist" => "wishlist",
                "password" => "password",
                _ => "profile"
            };
        }

        private async Task<AccountDashboardVM> BuildAccountDashboardAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstAsync(x => x.UserId == userId);
            var orders = await _context.Orders
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.OrderDate)
                .Select(x => new MyOrderItemVM
                {
                    OrderId = x.OrderId,
                    OrderDate = x.OrderDate,
                    TotalAmount = x.TotalAmount,
                    Status = x.Status,
                    PaymentStatus = x.PaymentStatus
                })
                .ToListAsync();
            var wishlist = HttpContext.Session.GetObjectFromJson<List<WishlistItemVM>>(WishlistSessionKey) ?? [];
            return new AccountDashboardVM
            {
                Profile = new ProfileVM
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone ?? string.Empty
                },
                Orders = orders,
                ChangePassword = new ChangePasswordVM(),
                Wishlist = wishlist
            };
        }

        [HttpGet]
        public async Task<IActionResult> MyOrders()
        {
            var userId = GetCurrentUserId();
            if (userId != null)
            {
                return RedirectToAction(nameof(Profile), new { tab = "orders" });
            }

            var guestOrderIds = HttpContext.Session.GetObjectFromJson<List<int>>(GUEST_ORDER_LIST_KEY) ?? [];
            var orders = await _context.Orders
                .Where(x => guestOrderIds.Contains(x.OrderId))
                .OrderByDescending(x => x.OrderDate)
                .Select(x => new MyOrderItemVM
                {
                    OrderId = x.OrderId,
                    OrderDate = x.OrderDate,
                    TotalAmount = x.TotalAmount,
                    Status = x.Status,
                    PaymentStatus = x.PaymentStatus
                })
                .ToListAsync();

            return View(orders);
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM model)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction(nameof(Login));

            if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới tối thiểu 6 ký tự.");
            }
            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Xác nhận mật khẩu không khớp.");
            }

            async Task<IActionResult> PasswordErrorViewAsync()
            {
                var dash = await BuildAccountDashboardAsync(userId.Value);
                dash.ChangePassword = model;
                ViewBag.ActiveTab = "password";
                return View("Profile", dash);
            }

            if (!ModelState.IsValid) return await PasswordErrorViewAsync();

            var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId.Value);
            if (user == null) return RedirectToAction(nameof(Login));

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                return await PasswordErrorViewAsync();
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _context.SaveChangesAsync();
            TempData["PasswordMessage"] = "Đổi mật khẩu thành công.";
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        [HttpGet]
        public async Task<IActionResult> OrderTracking(int id)
        {
            var userId = GetCurrentUserId();
            var query = _context.Orders
                .Include(x => x.OrderDetails)
                    .ThenInclude(x => x.Variant)
                        .ThenInclude(x => x.Product)
                .Where(x => x.OrderId == id);

            if (userId != null)
            {
                query = query.Where(x => x.UserId == userId.Value);
            }
            else
            {
                var guestOrderIds = HttpContext.Session.GetObjectFromJson<List<int>>(GUEST_ORDER_LIST_KEY) ?? [];
                if (!guestOrderIds.Contains(id))
                {
                    return RedirectToAction(nameof(MyOrders));
                }
            }

            var order = await query.FirstOrDefaultAsync();
            if (order == null) return RedirectToAction(nameof(MyOrders));

            var vm = new OrderTrackingVM
            {
                OrderId = order.OrderId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                TotalAmount = order.TotalAmount,
                Lines = order.OrderDetails.Select(x => new OrderTrackingLineVM
                {
                    ProductName = x.Variant.Product.Name,
                    Size = x.Variant.Size,
                    Quantity = x.Quantity,
                    Price = x.Price
                }).ToList(),
                Timeline = await _context.OrderStatusLogs
                    .Where(x => x.OrderId == id)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new OrderTrackingStatusLogVM
                    {
                        NewStatus = x.NewStatus,
                        Note = x.Note,
                        ChangedBy = x.ChangedBy,
                        CreatedAt = x.CreatedAt
                    }).ToListAsync()
            };

            return View(vm);
        }

        private int? GetCurrentUserId()
        {
            var claimValue = User.FindFirstValue("UserId");
            return int.TryParse(claimValue, out var userId) ? userId : null;
        }
    }
}