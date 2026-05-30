using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// كل محاولة دفع على رابط — ناجحة أو فاشلة.
    /// </summary>
    public class PaymentLinkTransaction : BaseEntity, ITenantEntity
    {
        public Guid PaymentLinkId { get; set; }
        public PaymentLink? PaymentLink { get; set; }

        // ── بيانات الدافع ─────────────────────────────────────────────────
        public string PayerName { get; set; } = string.Empty;
        public string PayerEmail { get; set; } = string.Empty;
        public string PayerPhone { get; set; } = string.Empty;

        // ── الدفع ─────────────────────────────────────────────────────────
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SAR";
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // ── بوابة الدفع ───────────────────────────────────────────────────
        public string GatewayName { get; set; } = string.Empty;    // مثلاً: Moyasar, HyperPay
        public string GatewayTransactionId { get; set; } = string.Empty;
        public string GatewayResponse { get; set; } = string.Empty; // JSON خام من البوابة

        // ── الأوردر الناتج عن الدفع ────────────────────────────────────────
        public Guid? GeneratedOrderId { get; set; }
        public Order? GeneratedOrder { get; set; }

        public DateTime? PaidAt { get; set; }
        public string FailureReason { get; set; } = string.Empty;

        // ── Tenant ────────────────────────────────────────────────────────
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
