using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Models.ViewModels;

public class BlogDetailVM
{
    public News CurrentPost { get; set; } = null!;
    public List<News> RecentPosts { get; set; } = [];
    public List<News> RelatedPosts { get; set; } = [];
}

public class BrandPageVM
{
    public Brand Brand { get; set; } = null!;
    public ProductListVM Listing { get; set; } = new();
}

//public class HomeCategoryShowcaseVM
//{
//    public int CategoryId { get; set; }
//    public string CategoryName { get; set; } = string.Empty;
//    /// <summary>Slug dùng cho /Product?category=...</summary>
//    public string CategorySlug { get; set; } = string.Empty;
//    public string BannerImageUrl { get; set; } = "/images/hero-placeholder.svg";
//    public List<ProductHomeVM> Products { get; set; } = [];
//}
public class HomeBrandShowcaseVM
{
    public int BrandId { get; set; }

    public string BrandName { get; set; } = string.Empty;

    public string BrandSlug { get; set; } = string.Empty;

    public string BannerImageUrl { get; set; } = "/images/hero-placeholder.svg";

    public List<ProductHomeVM> Products { get; set; } = [];
}

public class ForgotPasswordVM
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordVM
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
