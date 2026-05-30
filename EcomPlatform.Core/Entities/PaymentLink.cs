using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// رابط دفع مباشر — مستقل عن الأوردر، يُنشأ بمبلغ أو بمنتجات أو مرتبط بأوردر موجود.
    /// </summary>
    public class PaymentLink : BaseEntity, ITenantEntity
    {
        // ── معلومات أساسية ────────────────────────────────────────────────
        public string Code { get; set; } = string.Empty;          // كود فريد للرابط: PL-XXXXXX
        public string Title { get; set; } = string.Empty;          // عنوان يظهر للعميل
        public string Description { get; set; } = string.Empty;    // وصف اختياري

        // ── المبلغ ────────────────────────────────────────────────────────
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SAR";

        // ── نوع الربط ─────────────────────────────────────────────────────
        public PaymentLinkType LinkType { get; set; } = PaymentLinkType.FreeAmount;

        // ربط اختياري بأوردر موجود
        public Guid? OrderId { get; set; }
        public Order? Order { get; set; }

        // ── Expiry & Usage ────────────────────────────────────────────────
        public DateTime? ExpiresAt { get; set; }                   // null = لا ينتهي
        public int? MaxUses { get; set; }                          // null = غير محدود
        public int UsedCount { get; set; } = 0;

        // ── الحالة ────────────────────────────────────────────────────────
        public PaymentLinkStatus Status { get; set; } = PaymentLinkStatus.Active;

        // ── Redirect بعد الدفع ────────────────────────────────────────────
        public string SuccessRedirectUrl { get; set; } = string.Empty;
        public string FailureRedirectUrl { get; set; } = string.Empty;

        // ── بيانات إضافية ─────────────────────────────────────────────────
        public string Metadata { get; set; } = string.Empty;       // JSON حر للـ tenant

        // ── من أنشأ الرابط ────────────────────────────────────────────────
        public Guid? CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        // ── Tenant ────────────────────────────────────────────────────────
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // ── Navigation ────────────────────────────────────────────────────
        public ICollection<PaymentLinkItem> Items { get; set; } = new List<PaymentLinkItem>();
        public ICollection<PaymentLinkTransaction> Transactions { get; set; } = new List<PaymentLinkTransaction>();
    }
}
