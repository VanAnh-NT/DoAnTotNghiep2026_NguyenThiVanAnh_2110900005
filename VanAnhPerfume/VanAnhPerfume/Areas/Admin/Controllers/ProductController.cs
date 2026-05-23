using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;
using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductController : Controller
    {
        private static readonly JsonSerializerOptions JsonCamelOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly JsonSerializerOptions JsonReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const int PageSize = 20;
        private readonly VanAnhPerfumeContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductController(VanAnhPerfumeContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index(string? q, int? categoryId, int? brandId, string? status, string? stock, string? sort, int page = 1)
        {
            var query = _context.Products.AsNoTracking()
                .Include(x => x.Brand)
                .Include(x => x.Category)
                .Include(x => x.ProductVariants)
                    .ThenInclude(v => v.Inventory)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x =>
                    x.Name.Contains(term) ||
                    x.ProductVariants.Any(v => v.IsActive && v.Sku.Contains(term)));
            }
            if (categoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == categoryId.Value);
            }
            if (brandId.HasValue)
            {
                query = query.Where(x => x.BrandId == brandId.Value);
            }
            // Mặc định chỉ hiện SP đang hoạt động — "Xóa" chỉ soft-delete nên cần vậy mới thấy biến mất khỏi danh sách.
            if (string.IsNullOrWhiteSpace(status) || status.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => (x.Status ?? true));
            }
            else if (status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // không lọc theo Status
            }
            else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => !(x.Status ?? true));
            }
            if (!string.IsNullOrWhiteSpace(stock))
            {
                query = stock switch
                {
                    "instock" => query.Where(x =>
                        x.ProductVariants.Any(v =>
                            v.IsActive &&
                            (v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock) > 0)),

                    "low" => query.Where(x =>
                        x.ProductVariants.Any(v =>
                            v.IsActive &&
                            ((v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock) -
                             (v.Inventory != null ? v.Inventory.QuantityReserved : 0))
                            <= (v.Inventory != null ? v.Inventory.LowStockThreshold : 5))),

                    "out" => query.Where(x =>
                        !x.ProductVariants.Any(v => v.IsActive) ||
                        x.ProductVariants
                            .Where(v => v.IsActive)
                            .All(v => (v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock) <= 0)),

                    _ => query
                };
            }

query = sort switch
{
    "price_asc" => query.OrderBy(p => p.ProductVariants
        .Where(v => v.IsActive)
        .OrderBy(v => v.Price)
        .Select(v => v.Price)
        .FirstOrDefault()),

    "price_desc" => query.OrderByDescending(p => p.ProductVariants
        .Where(v => v.IsActive)
        .OrderBy(v => v.Price)
        .Select(v => v.Price)
        .FirstOrDefault()),

    "name_asc" => query.OrderBy(p => p.Name),
    "name_desc" => query.OrderByDescending(p => p.Name),
    _ => query.OrderByDescending(p => p.ProductId)
};

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var products = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new AdminProductIndexVM
                {
                    ProductId = x.ProductId,
                    Name = x.Name,
                    CategoryName = x.Category.Name,
                    BrandName = x.Brand.Name,
                    MainImage = x.MainImage,
                    IsActive = x.Status ?? true,
                    VariantCount = x.ProductVariants.Count(v => v.IsActive),

                                        TotalAvailable = x.ProductVariants
                        .Where(v => v.IsActive)
                        .Sum(v => v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock),

                                        IsLowStock = x.ProductVariants
                        .Where(v => v.IsActive)
                        .Any(v =>
                            ((v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock) -
                             (v.Inventory != null ? v.Inventory.QuantityReserved : 0))
                            <= (v.Inventory != null ? v.Inventory.LowStockThreshold : 5)),

                                        IsOutOfStock = !x.ProductVariants.Any(v => v.IsActive) ||
                        x.ProductVariants
                            .Where(v => v.IsActive)
                            .All(v => (v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock) <= 0)
                })
                .ToListAsync();

            await LoadSelections();
            ViewBag.Query = q; ViewBag.CategoryId = categoryId; ViewBag.BrandId = brandId;
            ViewBag.Status = status; ViewBag.Stock = stock; ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadSelections();
            return View("CreateEdit", new AdminProductWizardVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(AdminProductWizardVM model, List<IFormFile>? uploadImages)
        {
            await ValidateWizardModel(model, uploadImages);
            if (!ModelState.IsValid)
            {
                await LoadSelections();
                return View("CreateEdit", model);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();
                    var product = new Product
                    {
                        Name = model.Name.Trim(),
                        Slug = NormalizeSlug(model.Slug),
                        CategoryId = model.CategoryId,
                        BrandId = model.BrandId,
                        Gender = model.Gender,
                        Concentration = model.Concentration,
                        ShortDescription = model.ShortDescription,
                        Description = model.Description,
                        DetailSpecsJson = string.IsNullOrWhiteSpace(model.DetailSpecsJson) ? "[]" : model.DetailSpecsJson.Trim(),
                        IsFeatured = model.IsFeatured,
                        Status = model.IsActive,
                        ProductStatus = model.IsActive ? "Active" : "Draft",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();

                    var variants = DeserializeVariants(model.VariantsJson);
                    await UpsertVariantModelAsync(product.ProductId, variants, isEdit: false);
                    await UpsertImagesAsync(product.ProductId, product.Slug ?? "product", uploadImages ?? [], model.ExistingImagesJson);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                });
                TempData["Success"] = "Tạo sản phẩm thành công";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Không thể tạo sản phẩm: {ex.Message}");
                await LoadSelections();
                return View("CreateEdit", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(x => x.ProductVariants)
                    .ThenInclude(v => v.Inventory)
                .Include(x => x.ProductImages)
                .Include(x => x.VariantAttributes)
                    .ThenInclude(a => a.Values)
                .FirstOrDefaultAsync(x => x.ProductId == id);
            if (product == null) return NotFound();
            await LoadSelections();
            var vm = new AdminProductWizardVM
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Slug = product.Slug ?? string.Empty,
                CategoryId = product.CategoryId,
                BrandId = product.BrandId,
                Gender = product.Gender ?? "Unisex",
                Concentration = product.Concentration ?? "EDP",
                ShortDescription = product.ShortDescription,
                Description = product.Description,
                DetailSpecsJson = string.IsNullOrWhiteSpace(product.DetailSpecsJson) ? "[]" : product.DetailSpecsJson,
                IsFeatured = product.IsFeatured ?? false,
                IsActive = product.Status ?? true,
                AttributesJson = JsonSerializer.Serialize(product.VariantAttributes.Select(a => new VariantAttributeInput
                {
                    Name = a.Name,
                    Values = a.Values.Select(v => v.Value).ToList()
                }).ToList(), JsonCamelOptions),
                VariantsJson = JsonSerializer.Serialize(product.ProductVariants
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.VariantId)
                    .Select(v => new VariantRowInput
                    {
                        VariantId = v.VariantId,
                        VariantLabel = BuildVariantLabel(v),
                        Sku = v.Sku,
                        Price = v.Price,
                        OriginalPrice = v.OriginalPrice,
                        IsActive = v.IsActive,
                        QuantityAvailable = v.Inventory?.QuantityAvailable ?? v.Stock,
                        LowStockThreshold = v.Inventory?.LowStockThreshold ?? 5,
                        Values = BuildVariantValues(v)
                    }).ToList(), JsonCamelOptions),
                ExistingImagesJson = JsonSerializer.Serialize(product.ProductImages.OrderBy(i => i.SortOrder).Select(i => new
                {
                    imageId = i.ImageId,
                    imageUrl = i.ImageUrl,
                    isPrimary = i.IsPrimary,
                    sortOrder = i.SortOrder
                }).ToList(), JsonCamelOptions)
            };
            return View("CreateEdit", vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AdminProductWizardVM model, List<IFormFile>? uploadImages)
        {
            if (!model.ProductId.HasValue) return NotFound();
            await ValidateWizardModel(model, uploadImages, isEdit: true);
            if (!ModelState.IsValid)
            {
                await LoadSelections();
                return View("CreateEdit", model);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();
                    var product = await _context.Products.FirstOrDefaultAsync(x => x.ProductId == model.ProductId.Value);
                    if (product == null)
                    {
                        throw new InvalidOperationException("Sản phẩm không tồn tại.");
                    }
                    product.Name = model.Name.Trim();
                    product.Slug = NormalizeSlug(model.Slug);
                    product.CategoryId = model.CategoryId;
                    product.BrandId = model.BrandId;
                    product.Gender = model.Gender;
                    product.Concentration = model.Concentration;
                    product.ShortDescription = model.ShortDescription;
                    product.Description = model.Description;
                    product.DetailSpecsJson = string.IsNullOrWhiteSpace(model.DetailSpecsJson) ? "[]" : model.DetailSpecsJson.Trim();
                    product.IsFeatured = model.IsFeatured;
                    product.Status = model.IsActive;
                    product.ProductStatus = model.IsActive ? "Active" : "Draft";
                    product.UpdatedAt = DateTime.UtcNow;

                    var variants = DeserializeVariants(model.VariantsJson);
                    await UpsertVariantModelAsync(product.ProductId, variants, isEdit: true);
                    await UpsertImagesAsync(product.ProductId, product.Slug ?? "product", uploadImages ?? [], model.ExistingImagesJson);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                });
                TempData["Success"] = "Cập nhật sản phẩm thành công";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Không thể cập nhật sản phẩm: {ex.Message}");
                await LoadSelections();
                return View("CreateEdit", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                product.Status = false;
                product.ProductStatus = "Draft";
                product.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkDelete([FromForm] List<int>? ids)
        {
            if (ids == null || ids.Count == 0) return RedirectToAction(nameof(Index));
            var products = await _context.Products.Where(x => ids.Contains(x.ProductId)).ToListAsync();
            foreach (var p in products)
            {
                p.Status = false;
                p.ProductStatus = "Draft";
                p.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkToggleActive([FromForm] List<int>? ids)
        {
            if (ids == null || ids.Count == 0) return RedirectToAction(nameof(Index));
            var products = await _context.Products.Where(x => ids.Contains(x.ProductId)).ToListAsync();
            foreach (var p in products)
            {
                p.Status = !(p.Status ?? true);
                p.ProductStatus = (p.Status ?? true) ? "Active" : "Draft";
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Inventory(int id, int? variantId = null)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == id);
            if (product == null) return NotFound();
            var rawRows = await _context.ProductVariants
                .Where(v => v.ProductId == id && v.IsActive)
                .Include(v => v.Inventory)
                .OrderBy(v => v.Sku)
                .ToListAsync();
            var rows = rawRows.Select(v => new ProductInventoryRowVM
            {
                VariantId = v.VariantId,
                VariantLabel = BuildVariantLabel(v),
                Sku = v.Sku,
                QuantityAvailable = v.Inventory != null ? v.Inventory.QuantityAvailable : v.Stock,
                QuantityReserved = v.Inventory != null ? v.Inventory.QuantityReserved : 0,
                QuantitySold = v.Inventory != null ? v.Inventory.QuantitySold : 0,
                LowStockThreshold = v.Inventory != null ? v.Inventory.LowStockThreshold : 5
            }).ToList();

            var logs = _context.InventoryLogs.AsNoTracking().Where(x => rows.Select(r => r.VariantId).Contains(x.VariantId));
            if (variantId.HasValue) logs = logs.Where(x => x.VariantId == variantId);
            ViewBag.Product = product;
            ViewBag.Logs = await logs.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync();
            ViewBag.SelectedVariantId = variantId;
            return View(rows);
        }

        [HttpPost]
        public async Task<IActionResult> ImportStock(int productId, int variantId, int quantity, string? note)
        {
            if (quantity <= 0) return RedirectToAction(nameof(Inventory), new { id = productId });
            var variant = await _context.ProductVariants
                .Include(v => v.Inventory)
                .FirstOrDefaultAsync(v => v.VariantId == variantId && v.ProductId == productId && v.IsActive);
            if (variant == null) return RedirectToAction(nameof(Inventory), new { id = productId });
            var inv = await EnsureInventoryAsync(variant);
            inv.QuantityAvailable += quantity;
            inv.UpdatedAt = DateTime.UtcNow;
            _context.InventoryLogs.Add(new InventoryLog
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
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Inventory), new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> AdjustStock(int productId, int variantId, int newQuantity, string reason)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Inventory)
                .FirstOrDefaultAsync(v => v.VariantId == variantId && v.ProductId == productId && v.IsActive);
            if (variant == null) return RedirectToAction(nameof(Inventory), new { id = productId });
            var inv = await EnsureInventoryAsync(variant);
            var delta = newQuantity - inv.QuantityAvailable;
            inv.QuantityAvailable = newQuantity;
            inv.UpdatedAt = DateTime.UtcNow;
            _context.InventoryLogs.Add(new InventoryLog
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
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Inventory), new { id = productId });
        }

        private async Task LoadSelections()
        {
            ViewBag.Brands = await _context.Brands.OrderBy(x => x.Name).ToListAsync();
            ViewBag.Categories = await _context.Categories.Where(x => x.IsActive).OrderBy(x => x.ParentId).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync();
        }

        [HttpDelete("/Admin/Product/DeleteImage/{id:int}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.ProductImages.FindAsync(id);
            if (image == null) return NotFound();
            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ReorderImages(int productId, [FromBody] List<int> imageIds)
        {
            var images = await _context.ProductImages.Where(x => x.ProductId == productId).ToListAsync();
            for (var i = 0; i < imageIds.Count; i++)
            {
                var img = images.FirstOrDefault(x => x.ImageId == imageIds[i]);
                if (img != null)
                {
                    img.SortOrder = i + 1;
                }
            }
            if (imageIds.Count > 0)
            {
                var primary = images.FirstOrDefault(x => x.ImageId == imageIds[0]);
                if (primary != null)
                {
                    foreach (var img in images)
                    {
                        img.IsPrimary = img.ImageId == primary.ImageId;
                    }
                    var product = await _context.Products.FindAsync(productId);
                    if (product != null)
                    {
                        product.MainImage = primary.ImageUrl;
                    }
                }
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private async Task ValidateWizardModel(AdminProductWizardVM model, List<IFormFile>? uploadImages, bool isEdit = false)
        {
            var normalizedSlug = NormalizeSlug(model.Slug);
            var shouldCheckDuplicateSlug = true;

            if (isEdit && model.ProductId.HasValue)
            {
                var currentSlug = await _context.Products
                    .Where(x => x.ProductId == model.ProductId.Value)
                    .Select(x => x.Slug)
                    .FirstOrDefaultAsync();

                // Editing same product with unchanged slug should not be treated as duplicate.
                if (!string.IsNullOrWhiteSpace(currentSlug) &&
                    string.Equals(currentSlug, normalizedSlug, StringComparison.OrdinalIgnoreCase))
                {
                    shouldCheckDuplicateSlug = false;
                }
            }

            if (shouldCheckDuplicateSlug &&
                await _context.Products.AnyAsync(x => x.Slug == normalizedSlug && (!isEdit || x.ProductId != model.ProductId)))
            {
                ModelState.AddModelError(nameof(model.Slug), "Slug đã tồn tại.");
            }
            var variantRows = DeserializeVariants(model.VariantsJson);
            if (variantRows.Count(v => v.IsActive) == 0)
            {
                ModelState.AddModelError(nameof(model.VariantsJson), "Phải có ít nhất 1 biến thể active.");
            }
            foreach (var v in variantRows.Where(v => v.IsActive && v.Price > 0))
            {
                var sku = (v.Sku ?? string.Empty).Trim();
                if (sku.Length > 200)
                {
                    ModelState.AddModelError(nameof(model.VariantsJson), $"SKU quá dài ({sku.Length} ký tự, tối đa 200). Hãy rút ngắn SKU hoặc slug/thuộc tính tự sinh.");
                    break;
                }
            }
            var uploadedCount = uploadImages?.Count(x => x.Length > 0) ?? 0;
            var existingImageCount = JsonSerializer.Deserialize<List<JsonElement>>(model.ExistingImagesJson)?.Count ?? 0;
            if (uploadedCount + existingImageCount <= 0)
            {
                ModelState.AddModelError(nameof(model.ExistingImagesJson), "Phải có ít nhất 1 ảnh.");
            }
        }

        private static string NormalizeSlug(string input)
        {
            var text = input.Trim().ToLowerInvariant();
            text = Regex.Replace(text.Normalize(NormalizationForm.FormD), @"\p{Mn}", string.Empty);
            text = text.Replace("đ", "d");
            text = Regex.Replace(text, @"[^a-z0-9]+", "-");
            return Regex.Replace(text, @"-+", "-").Trim('-');
        }

        private async Task UpsertVariantModelAsync(int productId, List<VariantRowInput> rows, bool isEdit)
        {
            var existing = await _context.ProductVariants
                .Where(x => x.ProductId == productId)
                .Include(v => v.Inventory)
                .Include(v => v.ProductVariantAttributeValues)
                .ToListAsync();
            var existingIds = rows.Where(x => x.VariantId.HasValue).Select(x => x.VariantId!.Value).ToHashSet();
            foreach (var orphan in existing.Where(v => !existingIds.Contains(v.VariantId)))
            {
                var hasOrders = await _context.OrderDetails.AnyAsync(x => x.VariantId == orphan.VariantId);
                if (hasOrders)
                {
                    orphan.IsActive = false;
                }
                else
                {
                    _context.ProductVariantAttributeValues.RemoveRange(orphan.ProductVariantAttributeValues);
                    _context.ProductVariants.Remove(orphan);
                }
            }

            var attributes = DeserializeAttributes(rows);
            var oldAttrs = await _context.VariantAttributes.Where(x => x.ProductId == productId).Include(x => x.Values).ToListAsync();
            // Must clear join rows first: VariantAttributeValue is required by ProductVariantAttributeValue (NoAction on delete).
            var variantIdsForProduct = await _context.ProductVariants
                .Where(x => x.ProductId == productId)
                .Select(x => x.VariantId)
                .ToListAsync();
            var attributeLinks = await _context.ProductVariantAttributeValues
                .Where(x => variantIdsForProduct.Contains(x.ProductVariantId))
                .ToListAsync();
            _context.ProductVariantAttributeValues.RemoveRange(attributeLinks);
            _context.VariantAttributeValues.RemoveRange(oldAttrs.SelectMany(a => a.Values));
            _context.VariantAttributes.RemoveRange(oldAttrs);
            await _context.SaveChangesAsync();

            var valueMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in attributes)
            {
                var entity = new VariantAttribute { ProductId = productId, Name = attr.Name };
                _context.VariantAttributes.Add(entity);
                await _context.SaveChangesAsync();
                foreach (var value in attr.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var valEntity = new VariantAttributeValue { VariantAttributeId = entity.Id, Value = value };
                    _context.VariantAttributeValues.Add(valEntity);
                    await _context.SaveChangesAsync();
                    valueMap[$"{attr.Name}:{value}"] = valEntity.Id;
                }
            }

            foreach (var row in rows)
            {
                if (row.Price <= 0) continue;
                ProductVariant variant;
                if (row.VariantId is > 0 && existing.FirstOrDefault(v => v.VariantId == row.VariantId.Value) is { } found)
                {
                    variant = found;
                }
                else
                {
                    variant = new ProductVariant { ProductId = productId, CreatedAt = DateTime.UtcNow };
                    _context.ProductVariants.Add(variant);
                }

                variant.Sku = row.Sku.Trim();
                variant.Price = row.Price;
                variant.OriginalPrice = row.OriginalPrice;
                variant.IsActive = row.IsActive;
                variant.Size = row.Values.FirstOrDefault() ?? row.VariantLabel;
                variant.Color = row.Values.Skip(1).FirstOrDefault();
                variant.Material = row.Values.Skip(2).FirstOrDefault();

                await _context.SaveChangesAsync();
                await EnsureInventoryAsync(variant);
                if (variant.Inventory != null)
                {
                    variant.Inventory.QuantityAvailable = row.QuantityAvailable;
                    variant.Inventory.LowStockThreshold = row.LowStockThreshold;
                    variant.Inventory.UpdatedAt = DateTime.UtcNow;
                    variant.Stock = row.QuantityAvailable;
                }

                foreach (var attr in attributes)
                {
                    var value = row.Values.FirstOrDefault(v => attr.Values.Contains(v));
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var key = $"{attr.Name}:{value}";
                    if (!valueMap.TryGetValue(key, out var valueId)) continue;
                    _context.ProductVariantAttributeValues.Add(new ProductVariantAttributeValue
                    {
                        ProductVariantId = variant.VariantId,
                        VariantAttributeValueId = valueId
                    });
                }
            }
        }

        private async Task<Inventory> EnsureInventoryAsync(ProductVariant variant)
        {
            if (variant.Inventory != null) return variant.Inventory;
            var inv = await _context.Inventories.FirstOrDefaultAsync(x => x.ProductVariantId == variant.VariantId);
            if (inv != null)
            {
                variant.Inventory = inv;
                return inv;
            }
            inv = new Inventory
            {
                ProductVariantId = variant.VariantId,
                QuantityAvailable = Math.Max(0, variant.Stock),
                QuantityReserved = 0,
                QuantitySold = 0,
                LowStockThreshold = 5,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Inventories.Add(inv);
            await _context.SaveChangesAsync();
            variant.Inventory = inv;
            return inv;
        }

        private async Task UpsertImagesAsync(int productId, string productSlug, List<IFormFile> uploadImages, string existingImagesJson)
        {
            var existing = await _context.ProductImages.Where(x => x.ProductId == productId).ToListAsync();
            // Browser sends camelCase (imageId, imageUrl, …); bind case-insensitively or every ImageId is null and all DB rows are removed.
            var order = JsonSerializer.Deserialize<List<ImageOrderInput>>(existingImagesJson, JsonReadOptions) ?? [];

            var keepIds = order
                .Where(x => x.ImageId.HasValue)
                .Select(x => x.ImageId!.Value)
                .ToHashSet();

            var remove = existing.Where(x => !keepIds.Contains(x.ImageId)).ToList();
            _context.ProductImages.RemoveRange(remove);

            var existingById = existing.ToDictionary(x => x.ImageId);

            // 1) Update sort/primary for existing images (based on array order in ExistingImagesJson).
            foreach (var item in order.Select((x, idx) => new { x, idx }))
            {
                if (!item.x.ImageId.HasValue) continue;
                if (!existingById.TryGetValue(item.x.ImageId.Value, out var img)) continue;
                img.SortOrder = item.idx + 1;
                img.IsPrimary = item.x.IsPrimary;
            }

            // 2) Add URL images (remote http(s) URLs). We skip "blob:" URLs because they belong to local uploads.
            foreach (var item in order.Select((x, idx) => new { x, idx }))
            {
                if (item.x.ImageId.HasValue) continue;
                if (string.IsNullOrWhiteSpace(item.x.ImageUrl)) continue;
                if (item.x.ImageUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)) continue;

                _context.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = item.x.ImageUrl,
                    IsPrimary = item.x.IsPrimary,
                    SortOrder = item.idx + 1,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 3) Save uploaded files for items whose ImageUrl starts with "blob:".
            var uploaded = uploadImages.Where(x => x != null && x.Length > 0).ToList();
            var blobSlots = order
                .Select((x, idx) => new { x, idx })
                .Where(t =>
                    !t.x.ImageId.HasValue &&
                    !string.IsNullOrWhiteSpace(t.x.ImageUrl) &&
                    t.x.ImageUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (uploaded.Count > 0 && blobSlots.Count > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "products", productSlug);
                Directory.CreateDirectory(folder);

                for (var i = 0; i < uploaded.Count && i < blobSlots.Count; i++)
                {
                    var file = uploaded[i];
                    var slot = blobSlots[i];

                    var fileName = $"{Guid.NewGuid():N}.webp";
                    var fullPath = Path.Combine(folder, fileName);
                    var thumbPath = Path.Combine(folder, $"thumb_{fileName}");

                    using var image = await Image.LoadAsync(file.OpenReadStream());
                    image.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1200, 1200) }));
                    await image.SaveAsWebpAsync(fullPath, new WebpEncoder { Quality = 85 });

                    using var thumb = await Image.LoadAsync(file.OpenReadStream());
                    thumb.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(400, 400) }));
                    await thumb.SaveAsWebpAsync(thumbPath, new WebpEncoder { Quality = 80 });

                    _context.ProductImages.Add(new ProductImage
                    {
                        ProductId = productId,
                        ImageUrl = $"/uploads/products/{productSlug}/{fileName}",
                        IsPrimary = slot.x.IsPrimary,
                        SortOrder = slot.idx + 1,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Ensure exactly one primary image.
            var finalImages = await _context.ProductImages
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            if (!finalImages.Any()) return;

            var primary = finalImages.FirstOrDefault(x => x.IsPrimary) ?? finalImages[0];
            foreach (var img in finalImages) img.IsPrimary = img.ImageId == primary.ImageId;

            var product = await _context.Products.FindAsync(productId);
            if (product != null) product.MainImage = primary.ImageUrl;
        }

        private static List<VariantRowInput> DeserializeVariants(string json)
        {
            var list = JsonSerializer.Deserialize<List<VariantRowInput>>(string.IsNullOrWhiteSpace(json) ? "[]" : json, JsonReadOptions) ?? [];
            foreach (var r in list)
            {
                // JSON / draft có thể gửi variantId: 0 — coi như biến thể mới (tránh .First khi existing rỗng).
                if (r.VariantId is <= 0) r.VariantId = null;
            }
            return list;
        }

        private static List<VariantAttributeInput> DeserializeAttributes(List<VariantRowInput> rows)
        {
            var maxColumns = rows.Select(r => r.Values.Count).DefaultIfEmpty(0).Max();
            var attrs = new List<VariantAttributeInput>();
            for (var i = 0; i < maxColumns; i++)
            {
                var values = rows.Select(r => r.Values.Count > i ? r.Values[i] : null)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Cast<string>()
                    .ToList();
                if (!values.Any()) continue;
                attrs.Add(new VariantAttributeInput
                {
                    Name = i == 0 ? "Dung tích" : i == 1 ? "Màu sắc" : $"Thuộc tính {i + 1}",
                    Values = values
                });
            }
            return attrs;
        }

        private static string BuildVariantLabel(ProductVariant variant)
        {
            var tokens = new[] { variant.Color, variant.Size, variant.Material }.Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(" - ", tokens);
        }

        private static List<string> BuildVariantValues(ProductVariant variant)
        {
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(variant.Size)) values.Add(variant.Size);
            if (!string.IsNullOrWhiteSpace(variant.Color)) values.Add(variant.Color);
            if (!string.IsNullOrWhiteSpace(variant.Material)) values.Add(variant.Material);
            return values;
        }

        private class ImageOrderInput
        {
            public int? ImageId { get; set; }
            public bool IsPrimary { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}