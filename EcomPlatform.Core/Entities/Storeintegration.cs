using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// يمثل ربط متجر تاجر بمنصة خارجية (سلة، Shopify، Amazon، إلخ)
    /// </summary>
    public class StoreIntegration : BaseEntity
    {
        /// <summary>المنصة المرتبطة</summary>
        public MarketplacePlatform Platform { get; set; }

        /// <summary>اسم الربط — يظهر للتاجر في الداشبورد</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>حالة الربط</summary>
        public IntegrationStatus Status { get; set; } = IntegrationStatus.PendingSetup;

        // ── بيانات الـ Auth ──────────────────────────────────────────────────

        /// <summary>API Key أو Access Token (مشفر)</summary>
        public string? ApiKey { get; set; }

        /// <summary>API Secret (مشفر)</summary>
        public string? ApiSecret { get; set; }

        /// <summary>Refresh Token لو المنصة بتستخدم OAuth</summary>
        public string? RefreshToken { get; set; }

        /// <summary>Store URL — مطلوب لـ Shopify و WooCommerce وغيرهم</summary>
        public string? StoreUrl { get; set; }

        /// <summary>Store ID — مطلوب لبعض المنصات زي سلة وزد</summary>
        public string? ExternalStoreId { get; set; }

        /// <summary>Webhook Secret للتحقق من الـ webhooks الواردة</summary>
        public string? WebhookSecret { get; set; }

        /// <summary>تاريخ انتهاء الـ token</summary>
        public DateTime? TokenExpiresAt { get; set; }

        // ── إعدادات الـ Sync ─────────────────────────────────────────────────

        /// <summary>اتجاه الـ sync الافتراضي</summary>
        public SyncDirection SyncDirection { get; set; } = SyncDirection.BiDirectional;

        /// <summary>هل يتم sync المنتجات؟</summary>
        public bool SyncProducts { get; set; } = true;

        /// <summary>هل يتم sync الأوردرات؟</summary>
        public bool SyncOrders { get; set; } = true;

        /// <summary>هل يتم sync العملاء؟</summary>
        public bool SyncCustomers { get; set; } = true;

        /// <summary>هل يتم sync المخزون؟</summary>
        public bool SyncInventory { get; set; } = true;

        /// <summary>هل يتم sync الأسعار؟</summary>
        public bool SyncPrices { get; set; } = true;

        /// <summary>تكرار الـ auto sync بالدقائق (0 = manual only)</summary>
        public int AutoSyncIntervalMinutes { get; set; } = 0;

        /// <summary>آخر sync ناجح</summary>
        public DateTime? LastSyncAt { get; set; }

        /// <summary>آخر خطأ حصل</summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>عدد الأخطاء المتتالية</summary>
        public int ConsecutiveErrorCount { get; set; } = 0;

        // ── Tenant ───────────────────────────────────────────────────────────

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // ── Navigation ───────────────────────────────────────────────────────

        public ICollection<SyncLog> SyncLogs { get; set; } = [];
        public ICollection<WebhookEvent> WebhookEvents { get; set; } = [];
    }
}