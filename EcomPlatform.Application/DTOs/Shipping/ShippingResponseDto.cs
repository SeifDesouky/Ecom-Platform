using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Shipping
{
    public class ShippingZoneResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ShippingMethodResponseDto> Methods { get; set; } = new();
    }

    public class ShippingMethodResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ShippingType Type { get; set; }
        public decimal Cost { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxOrderAmount { get; set; }
        public int? EstimatedDaysMin { get; set; }
        public int? EstimatedDaysMax { get; set; }
        public bool IsActive { get; set; }
        public Guid ShippingZoneId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CalculateShippingDto
    {
        public Guid TenantId { get; set; }
        public decimal OrderAmount { get; set; }
    }
}