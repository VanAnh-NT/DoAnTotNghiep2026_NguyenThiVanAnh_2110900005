using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class Review
{
    public int ReviewId { get; set; }

    public int? UserId { get; set; }

    public int ProductId { get; set; }

    public int Rating { get; set; }

    public string? ReviewerName { get; set; }
    public string? ReviewerEmail { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Comment { get; set; }
    public string? Status { get; set; }
    public bool IsVerifiedPurchase { get; set; }
    public string? AdminReply { get; set; }
    public DateTime? AdminReplyAt { get; set; }
    public int HelpfulCount { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User? User { get; set; }
}
