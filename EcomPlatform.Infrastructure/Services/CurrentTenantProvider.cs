using EcomPlatform.Application.Common.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class CurrentTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; private set; }

        public void SetTenant(Guid tenantId)
        {
            TenantId = tenantId;
        }
    }
}