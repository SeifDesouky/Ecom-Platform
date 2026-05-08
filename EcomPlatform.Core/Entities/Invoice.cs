using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Invoice : BaseEntity, ITenantEntity
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime DueDate { get; set; }

        // Customer Info
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        // Navigation
        public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    }
}