using EcomPlatform.Core.Entities;

namespace EcomPlatform.Core.Interfaces.Repositories
{
    public interface IOrderRepository : IRepository<Order>
    {
        Task<Order?> FindByExternalIdAsync(string externalId, Guid storeIntegrationId);
    }
}