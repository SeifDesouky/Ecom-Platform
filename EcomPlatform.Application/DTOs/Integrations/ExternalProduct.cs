namespace EcomPlatform.Application.DTOs.Integrations
{
    public class ExternalProduct
    {
        public string ExternalId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Sku { get; init; }
        public decimal Price { get; init; }
        public decimal? CompareAtPrice { get; init; }
        public int StockQuantity { get; init; }
        public bool IsActive { get; init; }
        public string? ImageUrl { get; init; }
        public IReadOnlyList<string> Categories { get; init; } = [];
        public IReadOnlyList<ExternalProductVariant> Variants { get; init; } = [];
        public DateTime? UpdatedAt { get; init; }
    }

    public class ExternalProductVariant
    {
        public string ExternalId { get; init; } = string.Empty;
        public string? Sku { get; init; }
        public decimal Price { get; init; }
        public int StockQuantity { get; init; }
        public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
    }
}