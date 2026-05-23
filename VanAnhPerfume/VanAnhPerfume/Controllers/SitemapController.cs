using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;

namespace VanAnhPerfume.Controllers;

public class SitemapController(VanAnhPerfumeContext context) : Controller
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var staticUrls = new[]
        {
            $"{baseUrl}/",
            $"{baseUrl}/Home/Blog",
            $"{baseUrl}/Home/Contact",
            $"{baseUrl}/Home/Policy"
        };

        var products = await context.Products
            .AsNoTracking()
            .Select(x => new { x.ProductId })
            .ToListAsync();

        var news = await context.News
            .AsNoTracking()
            .Select(x => new { x.NewsId, x.CreatedAt })
            .ToListAsync();

        var brands = await context.Brands
            .AsNoTracking()
            .Select(x => x.Name)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var url in staticUrls)
        {
            sb.AppendLine($"  <url><loc>{url}</loc></url>");
        }

        foreach (var p in products)
        {
            sb.AppendLine($"  <url><loc>{baseUrl}/Product/Detail/{p.ProductId}</loc><lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod></url>");
        }

        foreach (var n in news)
        {
            var lm = (n.CreatedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
            sb.AppendLine($"  <url><loc>{baseUrl}/Home/BlogDetail/{n.NewsId}</loc><lastmod>{lm}</lastmod></url>");
        }

        foreach (var b in brands)
        {
            var slug = b.ToLower().Replace(" ", "-");
            sb.AppendLine($"  <url><loc>{baseUrl}/Home/Brand/{slug}</loc></url>");
        }

        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
