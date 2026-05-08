// ================================================================
// EcomPlatform.Infrastructure/Services/TenantValidationService.cs
// ================================================================
// بيستخدم في الـ Services لما تحتاج تتأكد يدوي من ملكية record
// قبل ما تعمل عليه حاجة (مثلاً: قبل Update بالـ Id)
// ================================================================
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class TenantValidationService
    {
        private readonly ITenantProvider _tenantProvider;

        public TenantValidationService(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// بيتحقق إن الـ entity بتاعة نفس الـ current tenant
        /// </summary>
        public void EnsureOwnership(ITenantEntity entity, string entityName = "Resource")
        {
            var currentTenantId = _tenantProvider.TenantId;

            // SuperAdmin مش محتاج ownership check
            if (currentTenantId == null) return;

            if (entity.TenantId != currentTenantId)
            {
                throw new UnauthorizedAccessException(
                    $"{entityName} does not belong to the current tenant.");
            }
        }

        /// <summary>
        /// بيرجع الـ TenantId الحالي أو يرمي exception لو مفيش
        /// </summary>
        public Guid RequiredTenantId()
        {
            return _tenantProvider.TenantId
                ?? throw new UnauthorizedAccessException(
                    "This operation requires an authenticated tenant.");
        }
    }
}
