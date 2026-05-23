using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReviewController(VanAnhPerfumeContext context) : Controller
{
    public async Task<IActionResult> Index(string? status, int? rating, string? product, DateTime? fromDate, DateTime? toDate, bool verifiedOnly = false, string? q = null, string? sort = null, int page = 1)
    {
        var query = context.Reviews.Include(x => x.Product).Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (rating.HasValue) query = query.Where(x => x.Rating == rating.Value);
        if (!string.IsNullOrWhiteSpace(product)) query = query.Where(x => x.Product.Name.Contains(product));
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(x => x.CreatedAt < toDate.Value.Date.AddDays(1));
        if (verifiedOnly) query = query.Where(x => x.IsVerifiedPurchase);
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => (x.ReviewerName ?? "").Contains(q) || (x.Content ?? x.Comment ?? "").Contains(q));
        }
        query = sort switch
        {
            "rating_desc" => query.OrderByDescending(x => x.Rating).ThenByDescending(x => x.CreatedAt),
            "rating_asc" => query.OrderBy(x => x.Rating).ThenByDescending(x => x.CreatedAt),
            "product" => query.OrderBy(x => x.Product.Name),
            _ => query.OrderByDescending(x => x.CreatedAt)
        };
        const int pageSize = 20;
        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        var reviews = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Stats = await context.Reviews.GroupBy(x => x.Status ?? "Pending").Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        ViewBag.Status = status; ViewBag.Rating = rating; ViewBag.Product = product; ViewBag.FromDate = fromDate; ViewBag.ToDate = toDate; ViewBag.VerifiedOnly = verifiedOnly; ViewBag.Query = q; ViewBag.Sort = sort;
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(reviews);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id);
        if (review == null) return RedirectToAction(nameof(Index));
        review.Status = "Approved";
        review.UpdatedAt = DateTime.UtcNow;
        await UpdateProductRatingAsync(review.ProductId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id);
        if (review == null) return RedirectToAction(nameof(Index));
        review.Status = "Rejected";
        review.UpdatedAt = DateTime.UtcNow;
        await UpdateProductRatingAsync(review.ProductId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reply(int id, string adminReply)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id);
        if (review == null) return RedirectToAction(nameof(Index));
        review.AdminReply = adminReply?.Trim();
        review.AdminReplyAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id);
        if (review == null) return RedirectToAction(nameof(Index));
        var productId = review.ProductId;
        context.Reviews.Remove(review);
        await UpdateProductRatingAsync(productId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> BulkApprove(List<int> ids)
    {
        var rows = await context.Reviews.Where(x => ids.Contains(x.ReviewId) && x.Status == "Pending").ToListAsync();
        var productIds = rows.Select(x => x.ProductId).Distinct().ToList();
        foreach (var row in rows) { row.Status = "Approved"; row.UpdatedAt = DateTime.UtcNow; }
        foreach (var productId in productIds) await UpdateProductRatingAsync(productId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> BulkDelete(List<int> ids)
    {
        var rows = await context.Reviews.Where(x => ids.Contains(x.ReviewId)).ToListAsync();
        var productIds = rows.Select(x => x.ProductId).Distinct().ToList();
        context.Reviews.RemoveRange(rows);
        foreach (var productId in productIds) await UpdateProductRatingAsync(productId);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task UpdateProductRatingAsync(int productId)
    {
        var product = await context.Products.FirstOrDefaultAsync(x => x.ProductId == productId);
        if (product == null) return;
        var approved = await context.Reviews.Where(x => x.ProductId == productId && x.Status == "Approved").ToListAsync();
        product.ReviewCount = approved.Count;
        product.AverageRating = approved.Count == 0 ? null : Math.Round((decimal)approved.Average(x => x.Rating), 1);
        product.UpdatedAt = DateTime.UtcNow;
    }
}
