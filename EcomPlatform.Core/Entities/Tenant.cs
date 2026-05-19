using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Tenant : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public TenantStatus Status { get; set; } = TenantStatus.Active;
        public DateTime? SubscriptionEndDate { get; set; }
        public string? VatNumber { get; set; } // ZATCA
        public decimal VatRate { get; set; } = 0.15m; // default 15%

        // Navigation Properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}