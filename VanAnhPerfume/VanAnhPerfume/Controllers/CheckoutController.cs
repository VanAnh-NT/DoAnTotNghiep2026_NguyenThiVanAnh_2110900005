using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Services;
using VanAnhPerfume.Models.ViewModels;
using VanAnhPerfume.Helpers; // Thêm để dùng Session Helper
using VanAnhPerfume.Data;
using System.Security.Claims;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Controllers
{
    public class CheckoutController(
        IOrderService orderService,
        IVnpayService vnpayService,
        VanAnhPerfumeContext context,
        ILogger<CheckoutController> logger,
        IOrderConfirmationNotifier orderConfirmationNotifier) : Controller
    {
        private const string CART_KEY = "VanAnhCart";
        private const string PROMOTION_KEY = "VanAnhPromotion";
        private const string BUY_NOW_BACKUP_KEY = "VanAnhCartBuyNowBackup";
        private const string BUY_NOW_ACTIVE_KEY = "VanAnhBuyNowActive";
        private const string GUEST_ORDER_KEY = "VanAnhGuestOrderId";
        private const string GUEST_ORDER_LIST_KEY = "VanAnhGuestOrderIds";
        private int CurrentUserId => int.TryParse(User.FindFirstValue("UserId"), out var id) ? id : 0;

        public IActionResult Index()
        {
            // Đọc giỏ hàng trực tiếp từ Session
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();

            if (!cart.Any()) return RedirectToAction("Index", "Cart");

            var model = new CheckoutVM
            {
                CartItems = cart,
            };
            var promotionState = HttpContext.Session.GetObjectFromJson<CheckoutVM>(PROMOTION_KEY);
            if (promotionState != null)
            {
                model.PromotionCode = promotionState.PromotionCode;
                model.PromotionId = promotionState.PromotionId;
                model.DiscountAmount = promotionState.DiscountAmount;
            }
            // Không tự áp khuyến mãi AutoApply khi vào trang — chỉ giảm giá khi khách đã áp mã (giỏ / nút Áp dụng).

            model.PaymentMethod = "COD";
            // Ô nhập mã để trống; khi đã áp mã qua session, dòng giảm giá vẫn đúng.
            model.PromotionCode = null;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon([FromBody] CouponRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return Json(new { success = false, message = "Mã khuyến mãi không hợp lệ." });
            }

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            var subtotal = cart.Sum(x => x.Total);
            var code = request.Code.Trim();

            var promotion = await context.Promotions.FirstOrDefaultAsync(x =>
                x.IsActive == true
                && x.StartDate <= DateTime.UtcNow
                && x.EndDate >= DateTime.UtcNow
                && x.Name == code);
            if (promotion == null)
            {
                return Json(new { success = false, message = "Mã khuyến mãi đã hết hạn hoặc không khả dụng." });
            }

            if (subtotal < (promotion.MinOrderValue ?? 0))
            {
                return Json(new { success = false, message = $"Đơn tối thiểu {(promotion.MinOrderValue ?? 0):N0} ₫ để dùng mã này." });
            }

            var discountAmount = subtotal * (promotion.DiscountPercent / 100m);

            discountAmount = Math.Min(discountAmount, subtotal);

            HttpContext.Session.SetObjectAsJson(PROMOTION_KEY, new CheckoutVM
            {
                PromotionId = promotion.PromoId,
                PromotionCode = promotion.Name,
                DiscountAmount = discountAmount
            });

            return Json(new
            {
                success = true,
                discountAmount,
                finalAmount = subtotal - discountAmount,
                message = "Áp dụng mã khuyến mãi thành công."
            });
        }

        [HttpPost]
        public async Task<IActionResult> Process(CheckoutVM model)
        {
            // Lấy lại giỏ hàng từ Session để đảm bảo dữ liệu mới nhất
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItemVM>>(CART_KEY) ?? new();
            model.CartItems = cart;
            if (!cart.Any())
            {
                TempData["Error"] = "Giỏ hàng đang trống, vui lòng thêm sản phẩm trước khi đặt hàng.";
                return RedirectToAction(nameof(Index));
            }

            var promotionState = HttpContext.Session.GetObjectFromJson<CheckoutVM>(PROMOTION_KEY);
            if (promotionState != null)
            {
                model.PromotionId = promotionState.PromotionId;
                model.PromotionCode = promotionState.PromotionCode;
                model.DiscountAmount = promotionState.DiscountAmount;
            }

            var normalizedPaymentMethod = (model.PaymentMethod ?? "COD").Trim().ToUpperInvariant();
            if (normalizedPaymentMethod is not ("COD" or "VNPAY"))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Phương thức thanh toán không hợp lệ.");
            }
            model.PaymentMethod = normalizedPaymentMethod;

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Vui lòng kiểm tra lại thông tin giao hàng.";
                return View("Index", model);
            }

            try
            {
                var orderId = await orderService.CreateOrderAsync(CurrentUserId > 0 ? CurrentUserId : (int?)null, model);
                HttpContext.Session.SetInt32(GUEST_ORDER_KEY, orderId);
                if (CurrentUserId <= 0)
                {
                    var guestOrderIds = HttpContext.Session.GetObjectFromJson<List<int>>(GUEST_ORDER_LIST_KEY) ?? [];
                    if (!guestOrderIds.Contains(orderId))
                    {
                        guestOrderIds.Insert(0, orderId);
                        if (guestOrderIds.Count > 20)
                        {
                            guestOrderIds = guestOrderIds.Take(20).ToList();
                        }
                        HttpContext.Session.SetObjectAsJson(GUEST_ORDER_LIST_KEY, guestOrderIds);
                    }
                }

                // Xóa giỏ hàng sau khi đặt thành công (và dữ liệu "mua ngay")
                HttpContext.Session.Remove(CART_KEY);
                HttpContext.Session.Remove(PROMOTION_KEY);
                HttpContext.Session.Remove(BUY_NOW_BACKUP_KEY);
                HttpContext.Session.Remove(BUY_NOW_ACTIVE_KEY);

                if (normalizedPaymentMethod == "VNPAY")
                {
                    var payUrl = vnpayService.CreatePaymentUrl(
                        orderId,
                        model.FinalAmount,
                        $"Thanh toan don hang #{orderId}",
                        HttpContext);
                    return Redirect(payUrl);
                }

                await orderConfirmationNotifier.NotifyCodOrderPlacedAsync(orderId, HttpContext.Request);

                return RedirectToAction(nameof(Payment), new { id = orderId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Checkout process failed.");
                TempData["Error"] = "Không thể tạo đơn hàng lúc này. Vui lòng thử lại sau ít phút.";
                return View("Index", model);
            }
        }

        public async Task<IActionResult> Payment(int id)
        {
            var query = context.Orders
                .Include(x => x.Payments)
                .AsNoTracking()
                .Where(x => x.OrderId == id);

            if (CurrentUserId > 0)
            {
                query = query.Where(x => x.UserId == CurrentUserId);
            }
            else
            {
                var guestOrderId = HttpContext.Session.GetInt32(GUEST_ORDER_KEY);
                if (guestOrderId != id)
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            var order = await query.FirstOrDefaultAsync();
            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var paymentStatus = order.Payments.OrderByDescending(x => x.PaymentId).Select(x => x.Status).FirstOrDefault() ?? "Pending";
            var paymentMethod = order.Payments.OrderByDescending(x => x.PaymentId).Select(x => x.PaymentMethod).FirstOrDefault() ?? "COD";
            return View(new PaymentVM
            {
                OrderId = id,
                Amount = order.TotalAmount,
                PaymentStatus = paymentStatus,
                PaymentMethod = paymentMethod,
                CustomerEmail = order.CustomerEmail
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateVnpayPayment(int id)
        {
            var query = context.Orders
                .Include(x => x.Payments)
                .Where(x => x.OrderId == id);

            if (CurrentUserId > 0)
            {
                query = query.Where(x => x.UserId == CurrentUserId);
            }
            else
            {
                var guestOrderId = HttpContext.Session.GetInt32(GUEST_ORDER_KEY);
                if (guestOrderId != id)
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            var order = await query.FirstOrDefaultAsync();
            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var latestPayment = order.Payments.OrderByDescending(x => x.PaymentId).FirstOrDefault();
            if (latestPayment != null && latestPayment.Status == "Paid")
            {
                return RedirectToAction(nameof(Payment), new { id });
            }
            var oldStatusForLog = latestPayment?.Status ?? "Pending";
            if (latestPayment == null)
            {
                latestPayment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = order.TotalAmount,
                    PaymentMethod = "VNPAY",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Payments.Add(latestPayment);
            }
            else
            {
                latestPayment.Amount = order.TotalAmount;
                latestPayment.PaymentMethod = "VNPAY";
                latestPayment.Status = "Pending";
                latestPayment.UpdatedAt = DateTime.UtcNow;
            }
            await context.SaveChangesAsync();
            context.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = latestPayment.PaymentId,
                OrderId = order.OrderId,
                OldStatus = oldStatusForLog,
                NewStatus = "Pending",
                Source = "System",
                Note = "Create VNPAY payment URL",
                LogDate = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var payUrl = vnpayService.CreatePaymentUrl(
                order.OrderId,
                order.TotalAmount,
                $"Thanh toan don hang #{order.OrderId}",
                HttpContext);

            return Redirect(payUrl);
        }

        [AllowAnonymous]
        public async Task<IActionResult> VnpayReturn()
        {
            if (!vnpayService.TryValidateResponse(Request.Query, out var data, out var responseCode))
            {
                logger.LogWarning("VNPAY Return invalid signature");
                return View("PaymentResult", new PaymentVM { Message = "Sai chữ ký VNPAY.", PaymentStatus = "Failed" });
            }

            if (!int.TryParse(data.GetValueOrDefault("vnp_TxnRef"), out var orderId))
            {
                return View("PaymentResult", new PaymentVM { Message = "Thiếu mã đơn hàng.", PaymentStatus = "Failed" });
            }

            var payment = await context.Payments
                .Include(x => x.Order)
                .OrderByDescending(x => x.PaymentId)
                .FirstOrDefaultAsync(x => x.OrderId == orderId);

            if (payment == null)
            {
                return View("PaymentResult", new PaymentVM { Message = "Không tìm thấy giao dịch.", PaymentStatus = "Failed" });
            }
            var oldStatus = payment.Status ?? "Pending";
            var oldOrderStatus = payment.Order.Status;
            payment.TransactionId = data.GetValueOrDefault("vnp_TransactionNo");
            payment.PaymentGatewayResponse = string.Join("&", data.Select(x => $"{x.Key}={x.Value}"));
            payment.UpdatedAt = DateTime.UtcNow;

            if (responseCode == "00")
            {
                payment.Status = "Paid";
                payment.PaidAt = DateTime.UtcNow;
                payment.Order.PaymentStatus = "Paid";
                payment.Order.Status = payment.Order.Status == "Pending" ? "Processing" : payment.Order.Status;
            }
            else
            {
                payment.Status = "Failed";
                payment.Order.PaymentStatus = "Failed";
            }

            context.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = payment.PaymentId,
                OrderId = orderId,
                OldStatus = oldStatus,
                NewStatus = payment.Status,
                Source = "VNPAY_Return",
                Note = $"ResponseCode: {responseCode}",
                LogDate = DateTime.UtcNow,
                RawData = string.Join("&", data.Select(x => $"{x.Key}={x.Value}")),
                Message = $"RETURN:{responseCode}"
            });
            if (oldOrderStatus != payment.Order.Status)
            {
                context.OrderStatusLogs.Add(new OrderStatusLog
                {
                    OrderId = orderId,
                    OldStatus = oldOrderStatus,
                    NewStatus = payment.Order.Status,
                    OldPaymentStatus = oldStatus,
                    NewPaymentStatus = payment.Status,
                    Note = "Order moved by VNPAY return",
                    ChangedBy = "VNPAY_Return",
                    CreatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            if (responseCode == "00")
            {
                await orderConfirmationNotifier.NotifyVnpayPaymentSucceededAsync(orderId, HttpContext.Request);
            }

            return View("PaymentResult", new PaymentVM
            {
                OrderId = orderId,
                Amount = payment.Amount,
                OrderCode = payment.Order.OrderCode,
                PaymentStatus = payment.Status ?? "Pending",
                Message = responseCode == "00" ? "Thanh toán thành công." : MapVnpayResponseMessage(responseCode),
                ResponseCode = responseCode,
                CustomerEmail = payment.Order.CustomerEmail
            });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VnpayIpn()
        {
            if (!vnpayService.TryValidateResponse(Request.Query, out var data, out var responseCode))
            {
                return Json(new { RspCode = "97", Message = "Invalid signature" });
            }

            if (!int.TryParse(data.GetValueOrDefault("vnp_TxnRef"), out var orderId))
            {
                return Json(new { RspCode = "01", Message = "Order not found" });
            }

            var payment = await context.Payments
                .Include(x => x.Order)
                .OrderByDescending(x => x.PaymentId)
                .FirstOrDefaultAsync(x => x.OrderId == orderId);

            if (payment == null)
            {
                return Json(new { RspCode = "01", Message = "Order not found" });
            }

            // idempotent: nếu đã paid thì nhận lại IPN vẫn trả success.
            if (payment.Status == "Paid")
            {
                return Json(new { RspCode = "00", Message = "OK" });
            }

            var oldStatus = payment.Status ?? "Pending";
            var oldOrderStatus = payment.Order.Status;
            payment.TransactionId = data.GetValueOrDefault("vnp_TransactionNo");
            payment.PaymentGatewayResponse = string.Join("&", data.Select(x => $"{x.Key}={x.Value}"));
            payment.UpdatedAt = DateTime.UtcNow;
            if (responseCode == "00")
            {
                payment.Status = "Paid";
                payment.PaidAt = DateTime.UtcNow;
                payment.Order.PaymentStatus = "Paid";
                payment.Order.Status = payment.Order.Status == "Pending" ? "Processing" : payment.Order.Status;
            }
            else
            {
                payment.Status = "Failed";
                payment.Order.PaymentStatus = "Failed";
            }

            context.PaymentLogs.Add(new PaymentLog
            {
                PaymentId = payment.PaymentId,
                OrderId = orderId,
                OldStatus = oldStatus,
                NewStatus = payment.Status,
                Source = "VNPAY_IPN",
                Note = $"ResponseCode: {responseCode}",
                LogDate = DateTime.UtcNow,
                RawData = string.Join("&", data.Select(x => $"{x.Key}={x.Value}")),
                Message = $"IPN:{responseCode}"
            });
            if (oldOrderStatus != payment.Order.Status)
            {
                context.OrderStatusLogs.Add(new OrderStatusLog
                {
                    OrderId = orderId,
                    OldStatus = oldOrderStatus,
                    NewStatus = payment.Order.Status,
                    OldPaymentStatus = oldStatus,
                    NewPaymentStatus = payment.Status,
                    Note = "Order moved by VNPAY IPN",
                    ChangedBy = "VNPAY_IPN",
                    CreatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            if (responseCode == "00")
            {
                await orderConfirmationNotifier.NotifyVnpayPaymentSucceededAsync(orderId, request: null);
            }

            logger.LogInformation("VNPAY IPN processed for order {OrderId} with code {Code}", orderId, responseCode);
            return Json(new { RspCode = "00", Message = "OK" });
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await context.Orders.Include(x => x.Payments).FirstOrDefaultAsync(x => x.OrderId == id);
            if (order == null) return RedirectToAction("Index", "Home");
            if (order.Status is "Completed" or "Cancelled") return RedirectToAction(nameof(Payment), new { id });
            var payment = order.Payments.OrderByDescending(x => x.PaymentId).FirstOrDefault();
            var oldPaymentStatus = payment?.Status ?? order.PaymentStatus;
            order.Status = "Cancelled";
            order.PaymentStatus = payment?.Status == "Paid" ? "Refunded" : "Failed";
            order.UpdatedAt = DateTime.UtcNow;
            if (payment != null)
            {
                payment.Status = payment.Status == "Paid" ? "Refunded" : "Failed";
                payment.RefundedAt = payment.Status == "Refunded" ? DateTime.UtcNow : null;
                payment.RefundAmount = payment.Status == "Refunded" ? payment.Amount : null;
                payment.RefundNote = "Cancelled by customer on payment result page";
                payment.UpdatedAt = DateTime.UtcNow;
                context.PaymentLogs.Add(new PaymentLog
                {
                    PaymentId = payment.PaymentId,
                    OrderId = id,
                    OldStatus = oldPaymentStatus,
                    NewStatus = payment.Status,
                    Source = "System",
                    Note = "Cancelled from payment result page",
                    LogDate = DateTime.UtcNow
                });
            }
            context.OrderStatusLogs.Add(new OrderStatusLog
            {
                OrderId = id,
                OldStatus = "Pending",
                NewStatus = "Cancelled",
                OldPaymentStatus = oldPaymentStatus,
                NewPaymentStatus = order.PaymentStatus,
                Note = "Cancelled from payment result page",
                ChangedBy = "System",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            return RedirectToAction(nameof(PaymentResult), new { id, failed = true, message = "Bạn đã hủy đơn hàng." });
        }

        [HttpGet]
        public async Task<IActionResult> PaymentResult(int id, bool failed = false, string? message = null)
        {
            var order = await context.Orders.Include(x => x.Payments).AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == id);
            if (order == null) return RedirectToAction("Index", "Home");
            var payment = order.Payments.OrderByDescending(x => x.PaymentId).FirstOrDefault();
            return View(new PaymentVM
            {
                OrderId = id,
                OrderCode = order.OrderCode,
                Amount = payment?.Amount ?? order.TotalAmount,
                PaymentMethod = payment?.PaymentMethod ?? "COD",
                PaymentStatus = payment?.Status ?? order.PaymentStatus ?? "Pending",
                Message = message ?? (failed ? "Thanh toán thất bại." : "Đặt hàng thành công!"),
                ResponseCode = null,
                CustomerEmail = order.CustomerEmail
            });
        }

        private static string MapVnpayResponseMessage(string code)
        {
            return code switch
            {
                "00" => "Thành công",
                "07" => "Trừ tiền thành công, giao dịch bị nghi ngờ",
                "09" => "Chưa đăng ký InternetBanking",
                "10" => "Xác thực sai quá 3 lần",
                "11" => "Hết hạn chờ thanh toán",
                "12" => "Thẻ bị khóa",
                "24" => "Giao dịch bị hủy",
                "51" => "Không đủ số dư",
                "65" => "Vượt hạn mức giao dịch ngày",
                "75" => "Ngân hàng đang bảo trì",
                "79" => "Sai mật khẩu quá số lần quy định",
                _ => "Lỗi khác"
            };
        }
    }

    public class CouponRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}