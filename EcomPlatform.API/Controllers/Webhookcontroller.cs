using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters.AliExpress;
using EcomPlatform.Infrastructure.Adapters.Amazon;
using EcomPlatform.Infrastructure.Adapters.eBay;
using EcomPlatform.Infrastructure.Adapters.ExpandCart;
using EcomPlatform.Infrastructure.Adapters.GoogleShopping;
using EcomPlatform.Infrastructure.Adapters.Meta;
using EcomPlatform.Infrastructure.Adapters.Noon;
using EcomPlatform.Infrastructure.Adapters.NoonExpress;
using EcomPlatform.Infrastructure.Adapters.Salla;
using EcomPlatform.Infrastructure.Adapters.Shopify;
using EcomPlatform.Infrastructure.Adapters.TikTokShop;
using EcomPlatform.Infrastructure.Adapters.WooCommerce;
using EcomPlatform.Infrastructure.Adapters.YouCan;
using EcomPlatform.Infrastructure.Adapters.Zid;
using EcomPlatform.Shared.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomPlatform.API.Controllers
{
    /// <summary>
    /// Webhook endpoint — يستقبل events من جميع المنصات.
    /// بدون [Authorize] — الأمان عبر signature verification لكل منصة.
    /// Flow: تحقق من الـ signature → احفظ في DB → شغّل الـ processor
    ///
    /// Signature methods per platform:
    ///   Salla          → HMAC-SHA256  (X-Salla-Signature, hex, sha256= prefix)
    ///   Zid            → HMAC-SHA256  (X-Zid-Signature, hex)
    ///   Shopify        → HMAC-SHA256  (X-Shopify-Hmac-Sha256, Base64)
    ///   WooCommerce    → HMAC-SHA256  (X-WC-Webhook-Signature, Base64)
    ///   TikTok Shop    → HMAC-SHA256  (X-Tiktok-Signature, hex)
    ///   Meta           → HMAC-SHA256  (X-Hub-Signature-256, sha256= prefix)
    ///   Amazon (SNS)   → No HMAC — verified via SNS message in processor
    ///   eBay           → SHA256 challenge token
    ///   Noon           → IP allowlist على الـ infrastructure
    ///   NoonExpress    → IP allowlist على الـ infrastructure (نفس Noon)
    ///   AliExpress     → HMAC-SHA256  (X-Aliexpress-Signature, hex)
    ///   Google         → OAuth token في الـ processor
    ///   YouCan         → HMAC-SHA256  (X-YouCan-Signature, hex)
    ///   ExpandCart     → HMAC-SHA256  (X-ExpandCart-Signature, Base64)
    /// </summary>
    [ApiController]
    [Route("api/webhooks")]
    public sealed class WebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        // ── Processors ────────────────────────────────────────────────────────
        private readonly SallaWebhookProcessor _sallaProcessor;
        private readonly ZidWebhookProcessor _zidProcessor;
        private readonly ShopifyWebhookProcessor _shopifyProcessor;
        private readonly WooCommerceWebhookProcessor _wooCommerceProcessor;
        private readonly TikTokShopWebhookProcessor _tikTokShopProcessor;
        private readonly MetaWebhookProcessor _metaProcessor;
        private readonly AmazonWebhookProcessor _amazonProcessor;
        private readonly EbayWebhookProcessor _ebayProcessor;
        private readonly NoonWebhookProcessor _noonProcessor;
        private readonly NoonExpressWebhookProcessor _noonExpressProcessor;
        private readonly AliExpressWebhookProcessor _aliExpressProcessor;
        private readonly GoogleShoppingWebhookProcessor _googleShoppingProcessor;
        private readonly YouCanWebhookProcessor _youCanProcessor;
        private readonly ExpandCartWebhookProcessor _expandCartProcessor;

        private readonly WebhookSettings _settings;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IUnitOfWork unitOfWork,
            SallaWebhookProcessor sallaProcessor,
            ZidWebhookProcessor zidProcessor,
            ShopifyWebhookProcessor shopifyProcessor,
            WooCommerceWebhookProcessor wooCommerceProcessor,
            TikTokShopWebhookProcessor tikTokShopProcessor,
            MetaWebhookProcessor metaProcessor,
            AmazonWebhookProcessor amazonProcessor,
            EbayWebhookProcessor ebayProcessor,
            NoonWebhookProcessor noonProcessor,
            NoonExpressWebhookProcessor noonExpressProcessor,
            AliExpressWebhookProcessor aliExpressProcessor,
            GoogleShoppingWebhookProcessor googleShoppingProcessor,
            YouCanWebhookProcessor youCanProcessor,
            ExpandCartWebhookProcessor expandCartProcessor,
            IOptions<WebhookSettings> webhookOptions,
            ILogger<WebhookController> logger)
        {
            _unitOfWork = unitOfWork;
            _sallaProcessor = sallaProcessor;
            _zidProcessor = zidProcessor;
            _shopifyProcessor = shopifyProcessor;
            _wooCommerceProcessor = wooCommerceProcessor;
            _tikTokShopProcessor = tikTokShopProcessor;
            _metaProcessor = metaProcessor;
            _amazonProcessor = amazonProcessor;
            _ebayProcessor = ebayProcessor;
            _noonProcessor = noonProcessor;
            _noonExpressProcessor = noonExpressProcessor;
            _aliExpressProcessor = aliExpressProcessor;
            _googleShoppingProcessor = googleShoppingProcessor;
            _youCanProcessor = youCanProcessor;
            _expandCartProcessor = expandCartProcessor;
            _settings = webhookOptions.Value;
            _logger = logger;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Salla  →  HMAC-SHA256  |  header: X-Salla-Signature (hex, sha256= prefix)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/salla</summary>
        [HttpPost("salla")]
        public async Task<IActionResult> Salla(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Salla-Signature");
            var eventType = GetHeader("X-Salla-Event") ?? "unknown";

            if (!VerifyHmacHex(rawBody, _settings.SallaSecret, signature))
            {
                _logger.LogWarning("[Webhook:Salla] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("Salla", eventType, rawBody,
                id => _sallaProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Zid  →  HMAC-SHA256  |  header: X-Zid-Signature (hex)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/zid</summary>
        [HttpPost("zid")]
        public async Task<IActionResult> Zid(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Zid-Signature");
            var eventType = GetHeader("X-Zid-Event") ?? "unknown";

            if (!VerifyHmacHex(rawBody, _settings.ZidSecret, signature))
            {
                _logger.LogWarning("[Webhook:Zid] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("Zid", eventType, rawBody,
                id => _zidProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Shopify  →  HMAC-SHA256  |  header: X-Shopify-Hmac-Sha256 (Base64)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/shopify</summary>
        [HttpPost("shopify")]
        public async Task<IActionResult> Shopify(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Shopify-Hmac-Sha256");
            var eventType = GetHeader("X-Shopify-Topic") ?? "unknown";

            if (!VerifyHmacBase64(rawBody, _settings.ShopifySecret, signature))
            {
                _logger.LogWarning("[Webhook:Shopify] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("Shopify", eventType, rawBody,
                id => _shopifyProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // WooCommerce  →  HMAC-SHA256  |  header: X-WC-Webhook-Signature (Base64)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/woocommerce</summary>
        [HttpPost("woocommerce")]
        public async Task<IActionResult> WooCommerce(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-WC-Webhook-Signature");
            var eventType = GetHeader("X-WC-Webhook-Topic") ?? "unknown";
            var storeUrl = GetHeader("X-WC-Webhook-Source");

            var secret = string.IsNullOrEmpty(storeUrl)
                ? _settings.WooCommerceDefaultSecret
                : await GetStoreWebhookSecretAsync(storeUrl, ct)
                      ?? _settings.WooCommerceDefaultSecret;

            if (!VerifyHmacBase64(rawBody, secret, signature))
            {
                _logger.LogWarning("[Webhook:WooCommerce] Invalid signature — Store: {Store}", storeUrl);
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("WooCommerce", eventType, rawBody,
                id => _wooCommerceProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // TikTok Shop  →  HMAC-SHA256  |  header: X-Tiktok-Signature (hex)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/tiktokshop</summary>
        [HttpPost("tiktokshop")]
        public async Task<IActionResult> TikTokShop(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Tiktok-Signature");
            var eventType = GetHeader("X-Tiktok-Event-Type") ?? "unknown";

            if (!VerifyHmacHex(rawBody, _settings.TikTokShopSecret, signature))
            {
                _logger.LogWarning("[Webhook:TikTokShop] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("TikTokShop", eventType, rawBody,
                id => _tikTokShopProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Meta (Instagram Shop + Facebook Shop + WhatsApp Catalog)
        // GET  → verification challenge (one-time setup)
        // POST → HMAC-SHA256  |  header: X-Hub-Signature-256 (sha256= prefix, hex)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>GET api/webhooks/meta — Meta verification challenge</summary>
        [HttpGet("meta")]
        public IActionResult MetaVerify(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (mode == "subscribe" &&
                verifyToken == _settings.MetaVerifyToken &&
                !string.IsNullOrEmpty(challenge))
            {
                _logger.LogInformation("[Webhook:Meta] Verification challenge passed");
                return Ok(challenge);
            }

            _logger.LogWarning("[Webhook:Meta] Verification challenge failed");
            return Forbid();
        }

        /// <summary>POST api/webhooks/meta</summary>
        [HttpPost("meta")]
        public async Task<IActionResult> Meta(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Hub-Signature-256");
            var eventType = ExtractMetaObject(rawBody);

            if (!VerifyHmacHex(rawBody, _settings.MetaAppSecret, signature))
            {
                _logger.LogWarning("[Webhook:Meta] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("Meta", eventType, rawBody,
                id => _metaProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Amazon SNS  →  No HMAC — verified in processor
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/amazon</summary>
        [HttpPost("amazon")]
        public async Task<IActionResult> Amazon(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var messageType = GetHeader("x-amz-sns-message-type") ?? "Notification";

            if (messageType.Equals("SubscriptionConfirmation", StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation("[Webhook:Amazon] SNS SubscriptionConfirmation — processor will handle");

            return await SaveAndProcessAsync("Amazon", messageType, rawBody,
                id => _amazonProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // eBay  →  SHA256 challenge token
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/ebay</summary>
        [HttpPost("ebay")]
        public async Task<IActionResult> Ebay(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var eventType = GetHeader("X-Event-Type") ?? "unknown";

            if (IsEbayChallenge(rawBody, out var challengeResponse))
                return Ok(new { challengeResponse });

            return await SaveAndProcessAsync("eBay", eventType, rawBody,
                id => _ebayProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Noon  →  IP allowlist (مفيش HMAC)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/noon</summary>
        [HttpPost("noon")]
        public async Task<IActionResult> Noon(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var eventType = GetHeader("X-Event-Type") ?? "unknown";

            return await SaveAndProcessAsync("Noon", eventType, rawBody,
                id => _noonProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Noon Express  →  IP allowlist (نفس Noon)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/noonexpress</summary>
        [HttpPost("noonexpress")]
        public async Task<IActionResult> NoonExpress(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var eventType = GetHeader("X-Event-Type") ?? "unknown";

            return await SaveAndProcessAsync("NoonExpress", eventType, rawBody,
                id => _noonExpressProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // AliExpress  →  HMAC-SHA256  |  header: X-Aliexpress-Signature (hex)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/aliexpress</summary>
        [HttpPost("aliexpress")]
        public async Task<IActionResult> AliExpress(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Aliexpress-Signature");
            var eventType = GetHeader("X-Aliexpress-Event") ?? "unknown";

            if (!VerifyHmacHex(rawBody, _settings.AliExpressSecret, signature))
            {
                _logger.LogWarning("[Webhook:AliExpress] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("AliExpress", eventType, rawBody,
                id => _aliExpressProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Google Shopping  →  OAuth token في الـ processor (Pub/Sub push)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/googleshopping</summary>
        [HttpPost("googleshopping")]
        public async Task<IActionResult> GoogleShopping(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var eventType = "product.status_change";

            return await SaveAndProcessAsync("GoogleShopping", eventType, rawBody,
                id => _googleShoppingProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // YouCan  →  HMAC-SHA256  |  header: X-YouCan-Signature (hex)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/youcan</summary>
        [HttpPost("youcan")]
        public async Task<IActionResult> YouCan(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-YouCan-Signature");
            var eventType = GetHeader("X-YouCan-Event") ?? "unknown";

            if (!VerifyHmacHex(rawBody, _settings.YouCanSecret, signature))
            {
                _logger.LogWarning("[Webhook:YouCan] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("YouCan", eventType, rawBody,
                id => _youCanProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // ExpandCart  →  HMAC-SHA256  |  header: X-ExpandCart-Signature (Base64)
        // نفس الـ processor لـ ExpandCart Gulf و ExpandCart Egypt
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>POST api/webhooks/expandcart</summary>
        [HttpPost("expandcart")]
        public async Task<IActionResult> ExpandCart(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-ExpandCart-Signature");
            var eventType = GetHeader("X-ExpandCart-Event") ?? "unknown";
            var storeUrl = GetHeader("X-ExpandCart-Store");

            var secret = string.IsNullOrEmpty(storeUrl)
                ? _settings.ExpandCartDefaultSecret
                : await GetStoreWebhookSecretAsync(storeUrl, ct)
                      ?? _settings.ExpandCartDefaultSecret;

            if (!VerifyHmacBase64(rawBody, secret, signature))
            {
                _logger.LogWarning("[Webhook:ExpandCart] Invalid signature — Store: {Store}", storeUrl);
                return Unauthorized(new { error = "Invalid signature" });
            }

            return await SaveAndProcessAsync("ExpandCart", eventType, rawBody,
                id => _expandCartProcessor.ProcessAsync(id, ct), ct);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Shared Handler
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// الـ common flow: احفظ الـ event → شغّل الـ processor → رجّع 200 OK دايماً
        /// (المنصات بتعمل retry لو ما ردّيتش بـ 2xx)
        /// </summary>
        private async Task<IActionResult> SaveAndProcessAsync(
            string platformName,
            string eventType,
            byte[] rawBody,
            Func<Guid, Task> process,
            CancellationToken ct)
        {
            _logger.LogInformation("[Webhook:{Platform}] Event received: {Event}",
                platformName, eventType);

            var webhookEvent = BuildWebhookEvent(rawBody, eventType);
            webhookEvent.StoreIntegrationId = ExtractStoreIntegrationId(rawBody);

            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                await process(webhookEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Webhook:{Platform}] Processing failed — Event: {Event}, Id: {Id}",
                    platformName, eventType, webhookEvent.Id);
            }

            return Ok(new { received = true });
        }

        // ══════════════════════════════════════════════════════════════════════
        // Signature Helpers
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>HMAC-SHA256 hex — بيدعم sha256= prefix (Salla, Zid, TikTok, Meta, AliExpress, YouCan)</summary>
        private static bool VerifyHmacHex(byte[] body, string? secret, string? receivedSig)
        {
            if (string.IsNullOrWhiteSpace(receivedSig) || string.IsNullOrWhiteSpace(secret))
                return false;

            var sig = receivedSig.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? receivedSig[7..]
                : receivedSig;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(sig.ToLowerInvariant()));
        }

        /// <summary>HMAC-SHA256 Base64 (Shopify, WooCommerce, ExpandCart)</summary>
        private static bool VerifyHmacBase64(byte[] body, string? secret, string? receivedSig)
        {
            if (string.IsNullOrWhiteSpace(receivedSig) || string.IsNullOrWhiteSpace(secret))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToBase64String(hmac.ComputeHash(body));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(receivedSig));
        }

        /// <summary>
        /// eBay challenge: SHA256(challengeCode + verificationToken + endpoint)
        /// https://developer.ebay.com/marketplace-account-deletion
        /// </summary>
        private bool IsEbayChallenge(byte[] rawBody, out string challengeResponse)
        {
            challengeResponse = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (!doc.RootElement.TryGetProperty("challengeCode", out var challengeEl))
                    return false;

                var challengeCode = challengeEl.GetString() ?? "";
                var endpoint = $"{Request.Scheme}://{Request.Host}/api/webhooks/ebay";
                var input = challengeCode + _settings.EbayVerificationToken + endpoint;

                using var sha = SHA256.Create();
                challengeResponse = Convert.ToHexString(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

                return true;
            }
            catch { return false; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Body / Payload Helpers
        // ══════════════════════════════════════════════════════════════════════

        private static WebhookEvent BuildWebhookEvent(byte[] rawBody, string eventType) => new()
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            RawPayload = Encoding.UTF8.GetString(rawBody),
            Status = WebhookEventStatus.Received,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        /// <summary>
        /// بيستخرج الـ StoreIntegrationId من الـ payload.
        /// لو مش موجود يرجع Guid.Empty والـ processor يتعامل معاه.
        /// </summary>
        private static Guid ExtractStoreIntegrationId(byte[] rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                // Salla / Zid / YouCan / ExpandCart: data.store_id
                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("store_id", out var storeEl) &&
                    Guid.TryParse(storeEl.GetString(), out var id))
                    return id;

                // Shopify: shop_id
                if (root.TryGetProperty("shop_id", out var shopEl) &&
                    Guid.TryParse(shopEl.GetString(), out var shopId))
                    return shopId;

                // Generic: store_id في root
                if (root.TryGetProperty("store_id", out var rootStore) &&
                    Guid.TryParse(rootStore.GetString(), out var rootId))
                    return rootId;
            }
            catch { }

            return Guid.Empty;
        }

        /// <summary>Meta: بيستخرج الـ object field (instagram / whatsapp_business_account / ...)</summary>
        private static string ExtractMetaObject(byte[] rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                if (doc.RootElement.TryGetProperty("object", out var obj))
                    return obj.GetString() ?? "unknown";
            }
            catch { }
            return "unknown";
        }

        private async Task<byte[]> ReadRawBodyAsync()
        {
            Request.EnableBuffering();
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            Request.Body.Position = 0;
            return ms.ToArray();
        }

        private string? GetHeader(string name)
            => Request.Headers.TryGetValue(name, out var val) ? val.ToString() : null;

        private async Task<string?> GetStoreWebhookSecretAsync(string storeUrl, CancellationToken ct)
        {
            var results = await _unitOfWork.StoreIntegrations
                .FindAsync(x => x.StoreUrl == storeUrl);
            return results.FirstOrDefault()?.WebhookSecret;
        }
    }
}