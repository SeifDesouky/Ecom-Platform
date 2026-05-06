using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class ShippingMethod : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ShippingType Type { get; set; } = ShippingType.Fixed;
        public decimal Cost { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxOrderAmount { get; set; }
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ShippingZoneId { get; set; }
        public ShippingZone? ShippingZone { get; set; }
    }
}