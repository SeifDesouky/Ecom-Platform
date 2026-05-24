namespace EcomPlatform.Application.DTOs.Integrations
{
    public class ExternalInventory
    {
        public string ExternalProductId { get; init; } = string.Empty;
        public string? ExternalVariantId { get; init; }
        public string? Sku { get; init; }
        public int Quantity { get; init; }
        public string? LocationId { get; init; }
    }
}