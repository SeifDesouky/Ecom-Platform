using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Interfaces.Repositories;
using EcomPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace EcomPlatform.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(Guid id) =>
            await ApplyIncludes(_dbSet).FirstOrDefaultAsync(e => e.Id == id);

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await ApplyIncludes(_dbSet).ToListAsync();

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
            await ApplyIncludes(_dbSet).Where(predicate).ToListAsync();

        public async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>> predicate,
            int skip,
            int take)
        {
            var query = ApplyIncludes(_dbSet).Where(predicate);
            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
            return (items, totalCount);
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
                _dbSet.Remove(entity);
        }

        public async Task<bool> ExistsAsync(Guid id) =>
            await _dbSet.AnyAsync(e => e.Id == id);

        protected virtual IQueryable<T> ApplyIncludes(IQueryable<T> query) => query;
    }

    public class ProductRepository : Repository<EcomPlatform.Core.Entities.Product>
    {
        public ProductRepository(AppDbContext context) : base(context) { }

        protected override IQueryable<EcomPlatform.Core.Entities.Product> ApplyIncludes(
            IQueryable<EcomPlatform.Core.Entities.Product> query) =>
            query.Include(p => p.Category).Include(p => p.Images);
    }
}