using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// منتجات مرتبطة بالـ PaymentLink (لو النوع ProductBased).
    /// </summary>
    public class PaymentLinkItem : BaseEntity
    {
        public Guid PaymentLinkId { get; set; }
        public PaymentLink? PaymentLink { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }                     // snapshot وقت إنشاء الرابط
        public string ProductName { get; set; } = string.Empty;    // snapshot كمان
    }
}
