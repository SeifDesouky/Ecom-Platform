using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class ShippingZone : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Navigation
        public ICollection<ShippingMethod> Methods { get; set; } = new List<ShippingMethod>();
    }
}