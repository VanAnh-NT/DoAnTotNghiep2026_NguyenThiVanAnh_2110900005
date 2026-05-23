using System.ComponentModel.DataAnnotations;

namespace VanAnhPerfume.Models.ViewModels;

public class ProductListVM
{
    public IEnumerable<ProductHomeVM> Products { get; set; } = [];
    public IEnumerable<BrandFilterItemVM> Brands { get; set; } = [];
    public IEnumerable<CategoryFilterItemVM> Categories { get; set; } = [];
    public string? Query { get; set; }
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 12;
    public string? Gender { get; set; }
    public string? Concentration { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? BrandSlug { get; set; }
}

public class BrandFilterItemVM
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class CategoryFilterItemVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? ParentId { get; set; }
}

public class CreateReviewVM
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MinLength(10)]
    [MaxLength(2000)]
    public string? Content { get; set; }
}

public class WishlistItemVM
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
}

public class SearchSuggestVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PrimaryImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class MiniCartVM
{
    public List<CartItemVM> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(x => x.Total);
    public int TotalQuantity => Items.Sum(x => x.Quantity);
}
