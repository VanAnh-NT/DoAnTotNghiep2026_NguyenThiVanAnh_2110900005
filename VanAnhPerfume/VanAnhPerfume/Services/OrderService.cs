using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels; // Thêm dòng này

namespace VanAnhPerfume.Services
{
    public interface IOrderService
    {
        Task<int> CreateOrderAsync(int? userId, CheckoutVM model);
    }

    public class OrderService(VanAnhPerfumeContext context) : IOrderService
    {
        public async Task<int> CreateOrderAsync(int? userId, CheckoutVM model)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            var createdOrderId = 0;
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await context.Database.BeginTransactionAsync();
                var todayPrefix = $"ORD-{DateTime.UtcNow:yyyyMMdd}-";
                var todayCount = await context.Orders.CountAsync(x => x.OrderCode != null && x.OrderCode.StartsWith(todayPrefix));
                var orderCode = $"{todayPrefix}{(todayCount + 1):00000}";
                var promotionSummary = model.PromotionId.HasValue
                    ? $"Promotion#{model.PromotionId} ({model.PromotionCode ?? "AUTO"}) -{model.DiscountAmount:N0}"
                    : null;

                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.Now,
                    TotalAmount = model.FinalAmount,
                    Status = "Pending",
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Address = model.Address,
                    PaymentStatus = "Pending",
                    AdminNote = promotionSummary,
                    OrderCode = orderCode,
                    CustomerEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    ShippingProvince = string.IsNullOrWhiteSpace(model.ShippingProvince) ? null : model.ShippingProvince.Trim(),
                    ShippingDistrict = string.IsNullOrWhiteSpace(model.ShippingDistrict) ? null : model.ShippingDistrict.Trim(),
                    SubTotal = model.TotalAmount,
                    DiscountAmount = model.DiscountAmount,
                    ShippingFee = 0,
                    CouponCode = model.PromotionCode,
                    Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
                    UpdatedAt = DateTime.UtcNow
                };

                context.Orders.Add(order);
                await context.SaveChangesAsync();

                foreach (var item in model.CartItems)
                {
                    var variant = await context.ProductVariants
                        .Include(v => v.Inventory)
                        .FirstOrDefaultAsync(v => v.VariantId == item.VariantId);
                    if (variant == null)
                    {
                        throw new InvalidOperationException("Biến thể sản phẩm không tồn tại.");
                    }
                    var inventory = variant.Inventory ?? new Inventory
                    {
                        ProductVariantId = variant.VariantId,
                        QuantityAvailable = Math.Max(0, variant.Stock),
                        QuantityReserved = 0,
                        QuantitySold = 0,
                        LowStockThreshold = 5,
                        UpdatedAt = DateTime.UtcNow
                    };
                    if (variant.Inventory == null)
                    {
                        context.Inventories.Add(inventory);
                    }
                    var sellable = inventory.QuantityAvailable - inventory.QuantityReserved;
                    if (sellable < item.Quantity)
                    {
                        throw new InvalidOperationException($"Sản phẩm {item.ProductName} chỉ còn {Math.Max(0, sellable)} sản phẩm.");
                    }
                    inventory.QuantityReserved += item.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;

                    var detail = new OrderDetail
                    {
                        OrderId = order.OrderId,
                        VariantId = item.VariantId,
                        ProductName = item.ProductName,
                        VariantName = item.Size,
                        Sku = variant.Sku,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    context.OrderDetails.Add(detail);
                    context.InventoryLogs.Add(new InventoryLog
                    {
                        VariantId = item.VariantId,
                        ActionType = "Sale",
                        ChangeType = "Sale",
                        QuantityDelta = -item.Quantity,
                        QuantityChange = -item.Quantity,
                        StockAfter = inventory.QuantityAvailable,
                        QuantityAfter = inventory.QuantityAvailable,
                        Note = $"Reserved for order #{order.OrderId}",
                        CreatedBy = userId?.ToString() ?? "Guest",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                context.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    Amount = model.FinalAmount,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
                await tx.CommitAsync();
                createdOrderId = order.OrderId;
            });

            return createdOrderId;
        }
    }
}