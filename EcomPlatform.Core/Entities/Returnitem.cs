using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// منتج واحد ضمن طلب الإرجاع.
    /// </summary>
    public class ReturnItem : BaseEntity
    {
        public Guid ReturnRequestId { get; set; }
        public ReturnRequest? ReturnRequest { get; set; }

        public Guid OrderItemId { get; set; }
        public OrderItem? OrderItem { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public string ProductName { get; set; } = string.Empty;   // snapshot
        public string ProductSKU { get; set; } = string.Empty;    // snapshot

        public int QuantityRequested { get; set; }
        public int QuantityApproved { get; set; }

        public decimal UnitPrice { get; set; }                    // snapshot
        public decimal TotalPrice => UnitPrice * QuantityApproved;
    }
}