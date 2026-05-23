using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<User> Users { get; }
        IGenericRepository<Product> Products { get; }
        // Thêm các thực thể khác tại đây khi cần
        Task<int> SaveAsync();
    }
}