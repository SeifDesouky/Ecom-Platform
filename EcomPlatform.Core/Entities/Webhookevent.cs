using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// أحداث الـ Webhook الواردة من المنصات الخارجية
    /// </summary>
    public class WebhookEvent : BaseEntity
    {
        /// <summary>نوع الحدث — زي "order.created" أو "product.updated"</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>حالة معالجة الـ webhook</summary>
        public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;

        /// <summary>الـ payload الخام من المنصة — JSON</summary>
        public string RawPayload { get; set; } = string.Empty;

        /// <summary>الـ IP اللي جاء منه الـ webhook</summary>
        public string? SourceIp { get; set; }

        /// <summary>الـ signature للتحقق من صحة الـ webhook</summary>
        public string? Signature { get; set; }

        /// <summary>هل اتحقق من الـ signature؟</summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>عدد محاولات المعالجة</summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>وقت آخر محاولة معالجة</summary>
        public DateTime? LastAttemptAt { get; set; }

        /// <summary>وقت المعالجة الناجحة</summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>رسالة الخطأ لو فشلت المعالجة</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>ID الـ entity في المنصة الخارجية (Order ID, Product ID, إلخ)</summary>
        public string? ExternalEntityId { get; set; }

        // ── Relations ────────────────────────────────────────────────────────

        public Guid StoreIntegrationId { get; set; }
        public StoreIntegration StoreIntegration { get; set; } = null!;

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}