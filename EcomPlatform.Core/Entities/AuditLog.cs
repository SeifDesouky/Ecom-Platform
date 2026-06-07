using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class AuditLog : BaseEntity, ITenantEntity
    {
        public string EntityName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public AuditAction Action { get; set; }
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;

        // Relations
        public Guid? UserId { get; set; }       // nullable — system/anonymous actions
        public User? User { get; set; }
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}