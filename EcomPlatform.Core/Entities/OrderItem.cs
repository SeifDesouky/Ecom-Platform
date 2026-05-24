using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class OrderItem : BaseEntity
    {
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string ExternalProductId { get; set; } = string.Empty;

        // Relations
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }
        public Guid ProductId { get; set; }
        public Product? Product { get; set; }
    }
}