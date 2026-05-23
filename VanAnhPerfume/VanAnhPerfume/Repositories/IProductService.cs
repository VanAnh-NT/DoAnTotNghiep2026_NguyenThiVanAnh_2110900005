using VanAnhPerfume.Models.ViewModels;

namespace VanAnhPerfume.Repositories
{
    public interface IProductService
    {
        Task<IEnumerable<ProductHomeVM>> GetProductsForHomeAsync();

        Task<ProductDetailVM?> GetProductDetailAsync(int id);
    }
}