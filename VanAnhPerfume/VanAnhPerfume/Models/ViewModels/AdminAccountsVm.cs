namespace VanAnhPerfume.Models.ViewModels;

public class AdminAccountRowVm
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminAccountsStatsVm
{
    public int Total { get; set; }
    public int AdminCount { get; set; }
    public int CustomerCount { get; set; }
}

public class AdminAccountsIndexVm
{
    public AdminAccountsStatsVm Stats { get; set; } = new();
    public List<AdminAccountRowVm> Rows { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? Q { get; set; }
    /// <summary>all | admin | customer</summary>
    public string RoleFilter { get; set; } = "all";

    /// <summary>Admin đang đăng nhập — không hiển thị nút hạ quyền chính mình.</summary>
    public int CurrentOperatorUserId { get; set; }
}
