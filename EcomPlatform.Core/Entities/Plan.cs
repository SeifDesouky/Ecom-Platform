using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Plan : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public decimal YearlyPrice { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsPopular { get; set; } = false;

        // Limits
        public int MaxProducts { get; set; }
        public int MaxOrders { get; set; }
        public int MaxCustomers { get; set; }
        public int MaxUsers { get; set; }
        public bool HasAnalytics { get; set; } = false;
        public bool HasAPI { get; set; } = false;
        public bool HasMultiCurrency { get; set; } = false;
        public bool HasCustomDomain { get; set; } = false;
        public bool HasPrioritySupport { get; set; } = false;

        // Navigation
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}