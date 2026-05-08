using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Customer : BaseEntity, ITenantEntity
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsEmailVerified { get; set; } = false;
        public string Notes { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; } = 0;
        public int TotalOrders { get; set; } = 0;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Navigation
        public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    }
}