using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Notification : BaseEntity, ITenantEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public string? ActionUrl { get; set; }
        public string? Icon { get; set; }

        // Relations
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}