using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// سجل كل عملية sync — ناجحة أو فاشلة
    /// </summary>
    public class SyncLog : BaseEntity
    {
        /// <summary>نوع الـ entity اللي اتعمل sync ليها</summary>
        public SyncEntityType EntityType { get; set; }

        /// <summary>اتجاه الـ sync</summary>
        public SyncDirection Direction { get; set; }

        /// <summary>حالة الـ sync</summary>
        public SyncStatus Status { get; set; } = SyncStatus.Pending;

        /// <summary>عدد العناصر اللي اتعالجت</summary>
        public int TotalRecords { get; set; } = 0;

        /// <summary>عدد العناصر الناجحة</summary>
        public int SuccessCount { get; set; } = 0;

        /// <summary>عدد العناصر الفاشلة</summary>
        public int FailedCount { get; set; } = 0;

        /// <summary>وقت بدء الـ sync</summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>وقت انتهاء الـ sync</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>مدة الـ sync بالثواني</summary>
        public double? DurationSeconds { get; set; }

        /// <summary>رسالة الخطأ لو فشل</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>تفاصيل إضافية — JSON</summary>
        public string? Details { get; set; }

        /// <summary>هل اتعمل manually أم auto؟</summary>
        public bool IsManual { get; set; } = false;

        // ── Relations ────────────────────────────────────────────────────────

        public Guid StoreIntegrationId { get; set; }
        public StoreIntegration StoreIntegration { get; set; } = null!;

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}