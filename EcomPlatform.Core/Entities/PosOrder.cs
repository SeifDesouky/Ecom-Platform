using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// فاتورة / عملية بيع POS.
    /// ترتبط بـ PosSession وترتبط اختيارياً بـ Order العادي (لو محتاجين توحيد).
    /// </summary>
    public class PosOrder : BaseEntity, ITenantEntity
    {
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public Guid PosSessionId { get; set; }
        public PosSession? PosSession { get; set; }

        /// <summary>رقم الفاتورة — يُولَّد تلقائياً مثل POS-20260529-0001</summary>
        public string ReceiptNumber { get; set; } = string.Empty;

        public PosOrderStatus Status { get; set; } = PosOrderStatus.Draft;

        // ── العميل (اختياري — ممكن بيع بدون عميل) ───────────────────────────
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;

        // ── الأسعار ───────────────────────────────────────────────────────────
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }

        /// <summary>المبلغ المدفوع نقداً (في حالة Mixed)</summary>
        public decimal CashPaid { get; set; }

        /// <summary>المبلغ المدفوع بالكارت (في حالة Mixed)</summary>
        public decimal CardPaid { get; set; }

        /// <summary>الباقي (Change) للعميل</summary>
        public decimal Change { get; set; }

        public PosPaymentMethod PaymentMethod { get; set; } = PosPaymentMethod.Cash;

        /// <summary>كوبون الخصم (لو استُخدم)</summary>
        public string? CouponCode { get; set; }

        public string Notes { get; set; } = string.Empty;

        public DateTime? CompletedAt { get; set; }

        /// <summary>الأوردر العادي المقابل لو تم ربطه (اختياري)</summary>
        public Guid? LinkedOrderId { get; set; }
        public Order? LinkedOrder { get; set; }

        // Navigation
        public ICollection<PosOrderItem> Items { get; set; } = new List<PosOrderItem>();
    }

    public class PosOrderItem : BaseEntity
    {
        public Guid PosOrderId { get; set; }
        public PosOrder? PosOrder { get; set; }

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public string ProductSKU { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        /// <summary>خصم على السطر (اختياري)</summary>
        public decimal LineDiscount { get; set; }

        public decimal TotalPrice { get; set; }
    }
}
