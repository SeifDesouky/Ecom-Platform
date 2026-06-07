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

        // ✅ جديد — بيتجاهل الـ query filters للسوبر ادمن
        public async Task<IEnumerable<T>> FindWithoutFilterAsync(Expression<Func<T, bool>> predicate) =>
            await _context.Set<T>()
                .IgnoreQueryFilters()
                .Where(predicate)
                .ToListAsync();

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

    public class ProductRepository : Repository<Core.Entities.Product>, IProductRepository
    {
        public ProductRepository(AppDbContext context) : base(context) { }

        protected override IQueryable<Core.Entities.Product> ApplyIncludes(
            IQueryable<Core.Entities.Product> query) =>
            query.Include(p => p.Category).Include(p => p.Images);

        public async Task<Core.Entities.Product?> FindByExternalIdAsync(
            string externalId,
            Guid storeIntegrationId) =>
            await ApplyIncludes(_dbSet)
                .FirstOrDefaultAsync(p =>
                    p.ExternalId == externalId &&
                    p.StoreIntegrationId == storeIntegrationId);
    }

    public class OrderRepository : Repository<Core.Entities.Order>, IOrderRepository
    {
        public OrderRepository(AppDbContext context) : base(context) { }

        protected override IQueryable<Core.Entities.Order> ApplyIncludes(
            IQueryable<Core.Entities.Order> query) =>
            query.Include(o => o.Items);

        public async Task<Core.Entities.Order?> FindByExternalIdAsync(
            string externalId,
            Guid storeIntegrationId) =>
            await ApplyIncludes(_dbSet)
                .FirstOrDefaultAsync(o =>
                    o.ExternalId == externalId &&
                    o.StoreIntegrationId == storeIntegrationId);
    }
}