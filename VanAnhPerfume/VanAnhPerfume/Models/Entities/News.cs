using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class News
{
    public int NewsId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Image { get; set; }

    public string? Slug { get; set; }
    public string? Excerpt { get; set; }
    public string? AuthorName { get; set; }
    public string? CategoryTag { get; set; }
    public int ViewCount { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string? NewsStatus { get; set; }

    public string? ThumbnailUrl { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public DateTime? PublishAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? Status { get; set; }
}
