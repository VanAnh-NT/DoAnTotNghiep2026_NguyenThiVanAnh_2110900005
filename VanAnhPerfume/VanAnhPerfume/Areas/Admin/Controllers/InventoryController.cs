using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class InventoryController(VanAnhPerfumeContext context) : Controller
{
    public async Task<IActionResult> Index(string? status, string? q, int? categoryId, int? brandId, string? sort, int page = 1)
    {
        var query = context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Brand)
            .Include(v => v.Product).ThenInclude(p => p.Category)
            .Include(v => v.Inventory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(v => v.Product.Name.Contains(term) || v.Sku.Contains(term));
        }
        if (categoryId.HasValue) query = query.Where(v => v.Product.CategoryId == categoryId);
        if (brandId.HasValue) query = query.Where(v => v.Product.BrandId == brandId);

        var rows = await query.ToListAsync();
        var dataQuery = rows.Select(v => new AdminInventoryRowVM
        {
            VariantId = v.VariantId,
            ProductId = v.ProductId,
            ProductName = v.Product.Name,
            BrandName = v.Product.Brand.Name,
            CategoryName = v.Product.Category.Name,
            VariantLabel = string.Join(" - ", new[] { v.Color, v.Size, v.Material }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Sku = v.Sku,
            ImageUrl = v.Product.MainImage,
            QuantityAvailable = v.Inventory?.QuantityAvailable ?? v.Stock,
            QuantityReserved = v.Inventory?.QuantityReserved ?? 0,
            QuantitySold = v.Inventory?.QuantitySold ?? 0,
            LowStockThreshold = v.Inventory?.LowStockThreshold ?? 5
        });

        if (!string.IsNullOrWhiteSpace(status))
        {
            dataQuery = status switch
            {
                "instock" => dataQuery.Where(v => !v.IsOutOfStock && !v.IsLowStock),
                "low" => dataQuery.Where(v => v.IsLowStock),
                "out" => dataQuery.Where(v => v.IsOutOfStock),
                _ => dataQuery
            };
        }
        dataQuery = sort switch
        {
            "sellable_asc" => dataQuery.OrderBy(v => v.Sellable),
            "sellable_desc" => dataQuery.OrderByDescending(v => v.Sellable),
            "sold_desc" => dataQuery.OrderByDescending(v => v.QuantitySold),
            _ => dataQuery.OrderBy(v => v.Sku)
        };
        const int pageSize = 30;
        var totalItems = dataQuery.Count();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var data = dataQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var all = await context.ProductVariants.Include(v => v.Inventory).ToListAsync();
        ViewBag.TotalSku = all.Count;
        ViewBag.InStockSku = all.Count(v => Math.Max(0, (v.Inventory?.QuantityAvailable ?? v.Stock) - (v.Inventory?.QuantityReserved ?? 0)) > 0);
        ViewBag.LowSku = all.Count(v =>
        {
            var sellable = Math.Max(0, (v.Inventory?.QuantityAvailable ?? v.Stock) - (v.Inventory?.QuantityReserved ?? 0));
            return sellable > 0 && sellable <= (v.Inventory?.LowStockThreshold ?? 5);
        });
        ViewBag.OutSku = all.Count(v => Math.Max(0, (v.Inventory?.QuantityAvailable ?? v.Stock) - (v.Inventory?.QuantityReserved ?? 0)) == 0);

        ViewBag.Categories = await context.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        ViewBag.Brands = await context.Brands.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        ViewBag.Status = status; ViewBag.Query = q; ViewBag.CategoryId = categoryId; ViewBag.BrandId = brandId; ViewBag.Sort = sort;
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(data);
    }

    [HttpPost]
    public async Task<IActionResult> ImportStock(int variantId, int quantity, string? note)
    {
        if (quantity <= 0) return RedirectToAction(nameof(Index));
        var variant = await context.ProductVariants.Include(v => v.Inventory).FirstOrDefaultAsync(v => v.VariantId == variantId);
        if (variant == null) return RedirectToAction(nameof(Index));
        var inv = await EnsureInventoryAsync(variant);
        inv.QuantityAvailable += quantity;
        inv.UpdatedAt = DateTime.UtcNow;
        context.InventoryLogs.Add(new InventoryLog
        {
            VariantId = variantId,
            ActionType = "Import",
            ChangeType = "Import",
            QuantityDelta = quantity,
            QuantityChange = quantity,
            StockAfter = inv.QuantityAvailable,
            QuantityAfter = inv.QuantityAvailable,
            Note = note,
            CreatedBy = User.Identity?.Name,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        await GenerateOutOfStockNotificationsAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AdjustStock(int variantId, int newQuantity, string reason)
    {
        var variant = await context.ProductVariants.Include(v => v.Inventory).FirstOrDefaultAsync(v => v.VariantId == variantId);
        if (variant == null) return RedirectToAction(nameof(Index));
        var inv = await EnsureInventoryAsync(variant);
        var delta = newQuantity - inv.QuantityAvailable;
        inv.QuantityAvailable = Math.Max(0, newQuantity);
        inv.UpdatedAt = DateTime.UtcNow;
        context.InventoryLogs.Add(new InventoryLog
        {
            VariantId = variantId,
            ActionType = "Adjustment",
            ChangeType = "Adjustment",
            QuantityDelta = delta,
            QuantityChange = delta,
            StockAfter = inv.QuantityAvailable,
            QuantityAfter = inv.QuantityAvailable,
            Note = reason,
            CreatedBy = User.Identity?.Name,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        await GenerateOutOfStockNotificationsAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> BulkImport()
    {
        return View(new List<BulkImportPreviewRowVM>());
    }

    [HttpPost]
    public async Task<IActionResult> BulkImportPreview(IFormFile? csvFile)
    {
        var rows = new List<BulkImportPreviewRowVM>();
        if (csvFile == null || csvFile.Length == 0) return View("BulkImport", rows);
        using var reader = new StreamReader(csvFile.OpenReadStream());
        _ = await reader.ReadLineAsync();
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = line.Split(',');
            var sku = cols.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
            var qtyText = cols.ElementAtOrDefault(1)?.Trim() ?? "0";
            var note = cols.ElementAtOrDefault(2)?.Trim().Trim('"');
            var row = new BulkImportPreviewRowVM { Sku = sku, Note = note };
            if (!int.TryParse(qtyText, out var qty) || qty <= 0)
            {
                row.IsValid = false; row.Error = "Quantity không hợp lệ";
                rows.Add(row); continue;
            }
            row.Quantity = qty;
            var variant = await context.ProductVariants.FirstOrDefaultAsync(x => x.Sku == sku);
            if (variant == null)
            {
                row.IsValid = false; row.Error = "SKU không tồn tại";
            }
            else
            {
                row.IsValid = true; row.VariantId = variant.VariantId;
            }
            rows.Add(row);
        }
        TempData["BulkImportRows"] = System.Text.Json.JsonSerializer.Serialize(rows);
        return View("BulkImport", rows);
    }

    [HttpPost]
    public async Task<IActionResult> BulkImportConfirm()
    {
        var json = TempData["BulkImportRows"]?.ToString();
        if (string.IsNullOrWhiteSpace(json)) return RedirectToAction(nameof(BulkImport));
        var rows = System.Text.Json.JsonSerializer.Deserialize<List<BulkImportPreviewRowVM>>(json) ?? [];
        foreach (var row in rows.Where(x => x.IsValid && x.VariantId.HasValue))
        {
            var variantId = row.VariantId.GetValueOrDefault();
            var variant = await context.ProductVariants.Include(v => v.Inventory).FirstOrDefaultAsync(v => v.VariantId == variantId);
            if (variant == null) continue;
            var inv = await EnsureInventoryAsync(variant);
            inv.QuantityAvailable += row.Quantity;
            inv.UpdatedAt = DateTime.UtcNow;
            context.InventoryLogs.Add(new InventoryLog
            {
                VariantId = variant.VariantId,
                ActionType = "Import",
                ChangeType = "Import",
                QuantityDelta = row.Quantity,
                QuantityChange = row.Quantity,
                StockAfter = inv.QuantityAvailable,
                QuantityAfter = inv.QuantityAvailable,
                Note = row.Note,
                CreatedBy = User.Identity?.Name,
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
        await GenerateOutOfStockNotificationsAsync();
        TempData["Success"] = "Đã nhập kho hàng loạt.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var csv = "SKU,Quantity,Note\nSP001-XANH-20,50,\"Nhap lo thang 12\"";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "inventory_import_template.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Logs(int? variantId, string? sku, string? changeType, DateTime? fromDate, DateTime? toDate)
    {
        var query = context.InventoryLogs.AsNoTracking().AsQueryable();
        if (variantId.HasValue) query = query.Where(x => x.VariantId == variantId);
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var ids = await context.ProductVariants.Where(x => x.Sku.Contains(sku)).Select(x => x.VariantId).ToListAsync();
            query = query.Where(x => ids.Contains(x.VariantId));
        }
        if (!string.IsNullOrWhiteSpace(changeType)) query = query.Where(x => (x.ChangeType ?? x.ActionType) == changeType);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAt < toDate.Value.Date.AddDays(1));

        var logs = await query.OrderByDescending(x => x.CreatedAt).Take(1000).ToListAsync();
        var variantMap = await context.ProductVariants.Include(x => x.Product)
            .Where(x => logs.Select(l => l.VariantId).Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId, x => new { x.Sku, ProductName = x.Product.Name, Label = $"{x.Color} - {x.Size} - {x.Material}" });
        ViewBag.VariantMap = variantMap;
        ViewBag.Sku = sku; ViewBag.ChangeType = changeType; ViewBag.FromDate = fromDate; ViewBag.ToDate = toDate;
        return View(logs);
    }

    [HttpGet]
    public async Task<IActionResult> ExportLogsCsv(string? sku, string? changeType, DateTime? fromDate, DateTime? toDate)
    {
        var query = context.InventoryLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var ids = await context.ProductVariants.Where(x => x.Sku.Contains(sku)).Select(x => x.VariantId).ToListAsync();
            query = query.Where(x => ids.Contains(x.VariantId));
        }
        if (!string.IsNullOrWhiteSpace(changeType)) query = query.Where(x => (x.ChangeType ?? x.ActionType) == changeType);
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAt < toDate.Value.Date.AddDays(1));
        var logs = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        var variantMap = await context.ProductVariants.Include(x => x.Product)
            .Where(x => logs.Select(l => l.VariantId).Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId, x => $"{x.Sku}|{x.Product.Name}");
        var sb = new StringBuilder();
        sb.AppendLine("Time,SKU,Product,Type,Change,After,Note,By");
        foreach (var l in logs)
        {
            var pair = variantMap.TryGetValue(l.VariantId, out var s) ? s : "|";
            var parts = pair.Split('|');
            sb.AppendLine($"{l.CreatedAt:yyyy-MM-dd HH:mm:ss},\"{parts.ElementAtOrDefault(0)}\",\"{parts.ElementAtOrDefault(1)}\",\"{(l.ChangeType ?? l.ActionType)}\",{(l.QuantityChange != 0 ? l.QuantityChange : l.QuantityDelta)},{(l.QuantityAfter != 0 ? l.QuantityAfter : l.StockAfter)},\"{l.Note}\",\"{l.CreatedBy}\"");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"inventory_logs_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task<Inventory> EnsureInventoryAsync(ProductVariant variant)
    {
        if (variant.Inventory != null) return variant.Inventory;
        var inventory = await context.Inventories.FirstOrDefaultAsync(x => x.ProductVariantId == variant.VariantId);
        if (inventory != null)
        {
            variant.Inventory = inventory;
            return inventory;
        }
        inventory = new Inventory
        {
            ProductVariantId = variant.VariantId,
            QuantityAvailable = Math.Max(0, variant.Stock),
            QuantityReserved = 0,
            QuantitySold = 0,
            LowStockThreshold = 5,
            UpdatedAt = DateTime.UtcNow
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();
        variant.Inventory = inventory;
        return inventory;
    }

    private async Task GenerateOutOfStockNotificationsAsync()
    {
        // EF cannot translate Math.Max + optional navigations in SQL; use equivalent logic:
        // sellable = Max(0, available - reserved) == 0  <=>  (no row: Stock<=0) OR (row: QA<=QR)
        var outRows = await context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.Inventory)
            .Where(v =>
                (v.Inventory == null && v.Stock <= 0) ||
                (v.Inventory != null && v.Inventory.QuantityAvailable <= v.Inventory.QuantityReserved))
            .Take(20)
            .ToListAsync();
        foreach (var row in outRows)
        {
            var title = "SKU hết hàng";
            var message = $"{row.Sku} - {row.Product.Name} đã hết hàng.";
            var exists = await context.AdminNotifications.AnyAsync(x => !x.IsRead && x.Title == title && x.Message == message);
            if (!exists)
            {
                context.AdminNotifications.Add(new AdminNotification
                {
                    Title = title,
                    Message = message,
                    Type = "Danger",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await context.SaveChangesAsync();
    }
}
