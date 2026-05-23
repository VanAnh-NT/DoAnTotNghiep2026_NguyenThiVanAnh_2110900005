using VanAnhPerfume.Data;
using VanAnhPerfume.Models.Entities;

namespace VanAnhPerfume.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly VanAnhPerfumeContext _context;
        public IGenericRepository<User> Users { get; private set; }
        public IGenericRepository<Product> Products { get; private set; }

        public UnitOfWork(VanAnhPerfumeContext context)
        {
            _context = context;
            Users = new GenericRepository<User>(_context);
            Products = new GenericRepository<Product>(_context);
        }

        public async Task<int> SaveAsync() => await _context.SaveChangesAsync();
        public void Dispose() => _context.Dispose();
    }
}