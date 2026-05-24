using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Salla.Models
{
    public class SallaProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("sku")]
        public string? Sku { get; init; }

        [JsonPropertyName("price")]
        public SallaPrice? Price { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("images")]
        public IReadOnlyList<SallaImage>? Images { get; init; }

        [JsonPropertyName("categories")]
        public IReadOnlyList<SallaCategory>? Categories { get; init; }

        [JsonPropertyName("variants")]
        public IReadOnlyList<SallaVariant>? Variants { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; init; }
    }

    public class SallaPrice
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;
    }

    public class SallaImage
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("main")]
        public bool IsMain { get; init; }
    }

    public class SallaCategory
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    public class SallaVariant
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("sku")]
        public string? Sku { get; init; }

        [JsonPropertyName("price")]
        public SallaPrice? Price { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        [JsonPropertyName("options")]
        public IReadOnlyList<SallaVariantOption>? Options { get; init; }
    }

    public class SallaVariantOption
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;
    }
}