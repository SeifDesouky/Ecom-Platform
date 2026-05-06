namespace EcomPlatform.Application.DTOs.Shipping
{
    public class CreateShippingZoneDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
    }
}