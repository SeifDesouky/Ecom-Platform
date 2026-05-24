using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Salla.Models
{
    public class SallaOrder
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("reference_id")]
        public string ReferenceId { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public SallaOrderStatus? Status { get; init; }

        [JsonPropertyName("amounts")]
        public SallaOrderAmounts? Amounts { get; init; }

        [JsonPropertyName("customer")]
        public SallaOrderCustomer? Customer { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<SallaOrderItem>? Items { get; init; }

        [JsonPropertyName("shipping")]
        public SallaShipping? Shipping { get; init; }

        [JsonPropertyName("date")]
        public SallaOrderDate? Date { get; init; }
    }

    public class SallaOrderStatus
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    public class SallaOrderAmounts
    {
        [JsonPropertyName("total")]
        public SallaPrice? Total { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;
    }

    public class SallaOrderCustomer
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("mobile")]
        public string? Mobile { get; init; }
    }

    public class SallaOrderItem
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("product")]
        public SallaOrderProduct? Product { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        [JsonPropertyName("amounts")]
        public SallaOrderItemAmounts? Amounts { get; init; }
    }

    public class SallaOrderProduct
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("sku")]
        public string? Sku { get; init; }
    }

    public class SallaOrderItemAmounts
    {
        [JsonPropertyName("price")]
        public SallaPrice? Price { get; init; }

        [JsonPropertyName("total")]
        public SallaPrice? Total { get; init; }
    }

    public class SallaShipping
    {
        [JsonPropertyName("address")]
        public SallaAddress? Address { get; init; }
    }

    public class SallaAddress
    {
        [JsonPropertyName("street")]
        public string? Street { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; init; }
    }

    public class SallaOrderDate
    {
        [JsonPropertyName("date")]
        public DateTime CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; init; }
    }
}