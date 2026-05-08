// ================================================================
// EcomPlatform.Infrastructure/Data/Interceptors/TenantEnforcementInterceptor.cs
// ================================================================
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EcomPlatform.Infrastructure.Data.Interceptors
{
    /// <summary>
    /// SaveChanges Interceptor بيضمن 3 حاجات:
    ///
    ///   1. Auto-inject TenantId على كل ITenantEntity جديدة (Added)
    ///      → لو developer نسي يحط TenantId، الـ interceptor بيحطه
    ///
    ///   2. Cross-tenant write protection (Modified / Deleted)
    ///      → لو حد حاول يعدل record مش بتاعه بالـ TenantId الحالي
    ///        بيرفع exception قبل ما يوصل للـ DB
    ///
    ///   3. TenantId tampering protection
    ///      → منع تغيير TenantId على record موجود
    /// </summary>
    public class TenantEnforcementInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantProvider _tenantProvider;

        public TenantEnforcementInterceptor(ITenantProvider tenantProvider)
        {
            _tenantProvider = tenantProvider;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            EnforceTenantRules(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            EnforceTenantRules(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // ────────────────────────────────────────────────────────────────

        private void EnforceTenantRules(DbContext? context)
        {
            if (context == null) return;

            var currentTenantId = _tenantProvider.TenantId;

            foreach (var entry in context.ChangeTracker.Entries<ITenantEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        HandleAdded(entry.Entity, currentTenantId);
                        break;

                    case EntityState.Modified:
                        HandleModified(entry, currentTenantId);
                        break;

                    case EntityState.Deleted:
                        HandleDeleted(entry, currentTenantId);
                        break;
                }
            }
        }

        /// <summary>
        /// على إضافة record جديد:
        /// - لو TenantId مش محطوط → يحطه من الـ current tenant
        /// - لو TenantId محطوط بـ value مختلف → يرفض (cross-tenant write)
        /// </summary>
        private static void HandleAdded(ITenantEntity entity, Guid? currentTenantId)
        {
            if (currentTenantId == null)
            {
                // SuperAdmin بيعمل operations على الـ platform نفسه، مش محتاج TenantId
                return;
            }

            if (entity.TenantId == null)
            {
                // Auto-inject: developer نسي، إحنا بنحطها
                entity.TenantId = currentTenantId;
            }
            else if (entity.TenantId != currentTenantId)
            {
                // محاولة كتابة على tenant مختلف — ممنوع
                throw new UnauthorizedAccessException(
                    $"Cross-tenant write attempt detected. " +
                    $"Current tenant: {currentTenantId}, " +
                    $"Entity tenant: {entity.TenantId}");
            }
        }

        /// <summary>
        /// على تعديل record موجود:
        /// - لازم يكون TenantId بتاع نفس الـ current tenant
        /// - منع تغيير الـ TenantId نفسه
        /// </summary>
        private static void HandleModified(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ITenantEntity> entry,
            Guid? currentTenantId)
        {
            if (currentTenantId == null) return; // SuperAdmin

            var entity = entry.Entity;

            // الـ record مش بتاع هذا الـ tenant
            if (entity.TenantId.HasValue && entity.TenantId != currentTenantId)
            {
                throw new UnauthorizedAccessException(
                    $"Cross-tenant modify attempt. " +
                    $"Current tenant: {currentTenantId}, " +
                    $"Entity tenant: {entity.TenantId}");
            }

            // منع تغيير TenantId على record موجود (TenantId tampering)
            var tenantIdProperty = entry.Property(nameof(ITenantEntity.TenantId));
            if (tenantIdProperty.IsModified)
            {
                var originalValue = tenantIdProperty.OriginalValue;
                var currentValue = tenantIdProperty.CurrentValue;

                if (originalValue != null && !Equals(originalValue, currentValue))
                {
                    throw new InvalidOperationException(
                        "TenantId cannot be changed after entity creation. " +
                        $"Original: {originalValue}, Attempted: {currentValue}");
                }
            }
        }

        /// <summary>
        /// على حذف record:
        /// - نفس منطق الـ Modified
        /// - لكن بالنسبة للـ Soft Delete، الـ state بيتحول Modified في الـ DbContext
        ///   فالـ check ده بيكون للـ Hard Delete (لو حصل)
        /// </summary>
        private static void HandleDeleted(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ITenantEntity> entry,
            Guid? currentTenantId)
        {
            if (currentTenantId == null) return; // SuperAdmin

            var entity = entry.Entity;

            if (entity.TenantId.HasValue && entity.TenantId != currentTenantId)
            {
                throw new UnauthorizedAccessException(
                    $"Cross-tenant delete attempt. " +
                    $"Current tenant: {currentTenantId}, " +
                    $"Entity tenant: {entity.TenantId}");
            }
        }
    }
}
