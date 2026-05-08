// ================================================================
// EcomPlatform.Core/Entities/Common/ITenantEntity.cs
// ================================================================
namespace EcomPlatform.Core.Entities.Common
{
    /// <summary>
    /// كل entity بيتعمله TenantId isolation يعمل implement لهذا الـ interface.
    /// الـ TenantEnforcementInterceptor بيستخدمه لـ auto-inject TenantId.
    /// </summary>
    public interface ITenantEntity
    {
        Guid? TenantId { get; set; }
    }
}
