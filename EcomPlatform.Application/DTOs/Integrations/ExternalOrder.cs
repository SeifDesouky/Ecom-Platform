namespace EcomPlatform.Application.DTOs.Integrations
{
    public class ExternalOrder
    {
        public string ExternalId { get; init; } = string.Empty;
        public string OrderNumber { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public ExternalCustomerInfo? Customer { get; init; }
        public IReadOnlyList<ExternalOrderItem> Items { get; init; } = [];
        public ExternalAddress? ShippingAddress { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public class ExternalOrderItem
    {
        public string ExternalProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public string? Sku { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
    }

    public class ExternalCustomerInfo
    {
        public string? ExternalId { get; init; }
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
    }

    public class ExternalAddress
    {
        public string? Street { get; init; }
        public string? City { get; init; }
        public string? Country { get; init; }
        public string? PostalCode { get; init; }
        public string? Phone { get; set; }
    }
}