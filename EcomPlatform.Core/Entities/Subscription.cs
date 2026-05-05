using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Subscription : BaseEntity
    {
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        public SubscriptionPeriod Period { get; set; } = SubscriptionPeriod.Monthly;
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; } = true;
        public DateTime? CancelledAt { get; set; }
        public string Notes { get; set; } = string.Empty;

        // Relations
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public Guid PlanId { get; set; }
        public Plan? Plan { get; set; }
    }
}