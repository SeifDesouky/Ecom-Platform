using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Warehouse : BaseEntity, ITenantEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;      // كود مختصر: WH-01
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsDefault { get; set; } = false;          // المستودع الرئيسي

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Navigation
        public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    }
}
