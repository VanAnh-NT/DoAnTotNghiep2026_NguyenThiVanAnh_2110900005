using System;
using System.Collections.Generic;

namespace VanAnhPerfume.Models.Entities;

public partial class OrderGifting
{
    public int GiftingId { get; set; }

    public int OrderId { get; set; }

    public bool? IncludeGiftWrap { get; set; }

    public string? GiftMessage { get; set; }

    public virtual Order Order { get; set; } = null!;
}
