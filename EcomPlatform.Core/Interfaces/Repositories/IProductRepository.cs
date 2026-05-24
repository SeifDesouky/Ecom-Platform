using EcomPlatform.Core.Entities;

namespace EcomPlatform.Core.Interfaces.Repositories
{
    public interface IProductRepository : IRepository<Product>
    {
        /// <summary>
        /// يجيب Product بالـ ExternalId + StoreIntegrationId (Salla product id مثلاً).
        /// يرجع null لو مش موجود.
        /// </summary>
        Task<Product?> FindByExternalIdAsync(string externalId, Guid storeIntegrationId);
    }
}