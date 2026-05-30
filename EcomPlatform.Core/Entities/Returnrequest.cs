using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// طلب إرجاع — يُنشأ من العميل أو الـ Admin أو تلقائياً عند Cancel.
    /// </summary>
    public class ReturnRequest : BaseEntity, ITenantEntity
    {
        public string ReturnNumber { get; set; } = string.Empty;   // RET-YYYYMMDD-XXXXXXXX

        // ── الأوردر المرتبط ───────────────────────────────────────────────
        public Guid OrderId { get; set; }
        public Order? Order { get; set; }

        // ── من طلب الإرجاع ────────────────────────────────────────────────
        public ReturnInitiator Initiator { get; set; } = ReturnInitiator.Customer;

        // ── سبب الإرجاع ───────────────────────────────────────────────────
        public ReturnReason Reason { get; set; }
        public string ReasonNote { get; set; } = string.Empty;    // تفاصيل إضافية

        // ── الحالة ────────────────────────────────────────────────────────
        public ReturnStatus Status { get; set; } = ReturnStatus.Pending;

        // ── المبالغ ───────────────────────────────────────────────────────
        public decimal RequestedAmount { get; set; }   // المبلغ اللي طلبه العميل
        public decimal ApprovedAmount { get; set; }    // المبلغ اللي وافق عليه الـ Admin

        // ── الاسترداد المالي ──────────────────────────────────────────────
        public RefundStatus RefundStatus { get; set; } = RefundStatus.Pending;
        public RefundMethod RefundMethod { get; set; } = RefundMethod.Manual;
        public DateTime? RefundedAt { get; set; }
        public string RefundGatewayTransactionId { get; set; } = string.Empty;
        public string RefundNote { get; set; } = string.Empty;

        // ── المخزون ───────────────────────────────────────────────────────
        public bool StockRestored { get; set; } = false;

        // ── من عالج الطلب ─────────────────────────────────────────────────
        public Guid? ReviewedById { get; set; }
        public User? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // ── Tenant ────────────────────────────────────────────────────────
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // ── Navigation ────────────────────────────────────────────────────
        public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    }
}