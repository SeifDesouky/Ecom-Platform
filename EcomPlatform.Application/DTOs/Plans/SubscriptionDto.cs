using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Plans
{
    public class CreateSubscriptionDto
    {
        public Guid TenantId { get; set; }
        public Guid PlanId { get; set; }
        public SubscriptionPeriod Period { get; set; } = SubscriptionPeriod.Monthly;
        public bool AutoRenew { get; set; } = true;
        public string Notes { get; set; } = string.Empty;
    }

    public class SubscriptionResponseDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public SubscriptionStatus Status { get; set; }
        public SubscriptionPeriod Period { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}