using Microsoft.EntityFrameworkCore;
using VanAnhPerfume.Data;

namespace VanAnhPerfume.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly VanAnhPerfumeContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(VanAnhPerfumeContext context)
        {
            _context = context;
            _dbSet = _context.Set<T>();
        }

        public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();
        public async Task<T> GetByIdAsync(object id)
        {
            var entity = await _dbSet.FindAsync(id);
            return entity ?? throw new KeyNotFoundException($"{typeof(T).Name} with id '{id}' was not found.");
        }
        public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);
        public void Update(T entity) => _dbSet.Update(entity);
        public void Delete(T entity) => _dbSet.Remove(entity);
    }
}