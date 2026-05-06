using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class InvoiceItem : BaseEntity
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Relations
        public Guid InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }
    }
}