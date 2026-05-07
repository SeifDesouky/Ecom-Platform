namespace EcomPlatform.Application.Common.Interfaces
{
    public interface ITenantProvider
    {
        Guid? TenantId { get; }
        void SetTenant(Guid tenantId);
    }
}