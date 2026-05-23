using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Models.ViewModels;

public static class CustomerTierHelper
{
    public const decimal SilverMin = 1_000_000m;
    public const decimal GoldAbove = 5_000_000m;

    /// <summary>Tier from spend on orders that are Completed and Paid.</summary>
    public static (string Key, string Label, string CssClass) GetTier(decimal completedPaidSpend)
    {
        if (completedPaidSpend < SilverMin)
            return ("new", "Khách hàng mới", "bg-secondary");
        if (completedPaidSpend <= GoldAbove)
            return ("silver", "Khách thân thiết", "bg-info text-dark");
        return ("gold", "Khách VIP", "bg-warning text-dark");
    }
}

public class AdminCustomerStatsVm
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Locked { get; set; }
    public int NewThisMonth { get; set; }
}

public class AdminCustomerRowVm
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    public int OrderCount { get; set; }
    public decimal PaidSpend { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}

public class AdminCustomerIndexVm
{
    public AdminCustomerStatsVm Stats { get; set; } = new();
    public List<AdminCustomerRowVm> Rows { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? Q { get; set; }
    public string? StatusFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Sort { get; set; } = "created_desc";
}

public class AdminCustomerSidebarStatsVm
{
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public double CompletionRatePercent { get; set; }
    public decimal TotalPaidSpend { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int ReviewedProductCount { get; set; }
    public (string Key, string Label, string CssClass) Tier { get; set; }
}

public class AdminCustomerOrdersSummaryVm
{
    public int TotalOrders { get; set; }
    public decimal TotalPaidSpend { get; set; }
    public int CancelledOrders { get; set; }
    public double CompletionRatePercent { get; set; }
}

public class AdminCustomerOrderRowVm
{
    public int OrderId { get; set; }
    public string? OrderCode { get; set; }
    public DateTime? OrderDate { get; set; }
    public string ProductSummary { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }
}

public class AdminCustomerReviewRowVm
{
    public int ReviewId { get; set; }
    public string ProductName { get; set; } = "";
    public int Rating { get; set; }
    public string? Content { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminCustomerDetailVm
{
    public User User { get; set; } = null!;
    public IReadOnlyList<AddressBook> Addresses { get; set; } = Array.Empty<AddressBook>();
    public AdminCustomerSidebarStatsVm Sidebar { get; set; } = new();
    public List<AdminCustomerOrderRowVm> Orders { get; set; } = [];
    public int OrdersPage { get; set; } = 1;
    public int OrdersTotalPages { get; set; } = 1;
    public int OrdersTotalCount { get; set; }
    public AdminCustomerOrdersSummaryVm OrdersSummary { get; set; } = new();
    public List<AdminCustomerReviewRowVm> Reviews { get; set; } = [];
    public bool WishlistInDatabase { get; set; }
}
