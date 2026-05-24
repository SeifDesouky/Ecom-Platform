namespace EcomPlatform.Shared.Settings
{
    public sealed class WebhookSettings
    {
        // ── Arab Platforms ────────────────────────────────────────────────────

        /// <summary>HMAC-SHA256 secret — header: X-Salla-Signature (hex, sha256= prefix)</summary>
        public string SallaSecret { get; init; } = string.Empty;

        /// <summary>HMAC-SHA256 secret — header: X-Zid-Signature (hex)</summary>
        public string ZidSecret { get; init; } = string.Empty;

        // ── Global E-commerce Platforms ───────────────────────────────────────

        /// <summary>HMAC-SHA256 secret — header: X-Shopify-Hmac-Sha256 (Base64)</summary>
        public string ShopifySecret { get; init; } = string.Empty;

        /// <summary>
        /// HMAC-SHA256 fallback secret — header: X-WC-Webhook-Signature (Base64)
        /// ملحوظة: الـ secret الحقيقي بيتجاب per-store من StoreIntegration.WebhookSecret
        /// الـ default ده fallback لو الـ store مش موجود في DB
        /// </summary>
        public string WooCommerceDefaultSecret { get; init; } = string.Empty;

        /// <summary>HMAC-SHA256 secret — header: X-Tiktok-Signature (hex)</summary>
        public string TikTokShopSecret { get; init; } = string.Empty;

        // ── Meta (Instagram Shop + Facebook Shop + WhatsApp Catalog) ──────────

        /// <summary>
        /// HMAC-SHA256 App Secret — header: X-Hub-Signature-256 (sha256= prefix, hex)
        /// نفس الـ App Secret في Meta Developer Console لكل المنصات الثلاثة
        /// </summary>
        public string MetaAppSecret { get; init; } = string.Empty;

        /// <summary>
        /// Token بتحطه أنت بنفسك في Meta Developer Console
        /// بيتبعت في GET api/webhooks/meta?hub.verify_token=... عند الـ setup
        /// </summary>
        public string MetaVerifyToken { get; init; } = string.Empty;

        // ── Marketplaces ──────────────────────────────────────────────────────

        /// <summary>
        /// Amazon SNS — مفيش HMAC
        /// التحقق بيتم داخل AmazonWebhookProcessor عبر SNS message verification
        /// الـ field ده reserved للـ future use أو custom validation
        /// </summary>
        public string AmazonSnsTopicArn { get; init; } = string.Empty;

        /// <summary>
        /// eBay Verification Token — بتحطه في eBay Developer Console
        /// بيُستخدم في SHA256(challengeCode + token + endpoint) للرد على الـ challenge
        /// https://developer.ebay.com/marketplace-account-deletion
        /// </summary>
        public string EbayVerificationToken { get; init; } = string.Empty;

        /// <summary>HMAC-SHA256 secret — header: X-Aliexpress-Signature (hex)</summary>
        public string AliExpressSecret { get; init; } = string.Empty;

        /// <summary>
        /// Noon — مفيش HMAC signature
        /// التحقق عبر IP allowlist على الـ infrastructure (firewall / API gateway)
        /// </summary>
        public string NoonAllowedIpRange { get; init; } = string.Empty;

        /// <summary>
        /// Google Shopping — OAuth token في الـ processor
        /// الـ field ده للـ topic name اللي Pub/Sub بيبعت عليه
        /// </summary>
        public string GoogleShoppingPubSubTopic { get; init; } = string.Empty;
        public string YouCanSecret { get; set; } = string.Empty;
        public string ExpandCartDefaultSecret { get; set; } = string.Empty;

    }
}