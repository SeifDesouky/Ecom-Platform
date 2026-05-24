using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters.Salla;
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
    /// Webhook endpoint — يستقبل events من Salla و Zid.
    /// بدون [Authorize] — الأمان عبر HMAC signature verification.
    /// Flow: تحقق من الـ signature → احفظ في DB → شغّل الـ processor
    /// </summary>
    [ApiController]
    [Route("api/webhooks")]
    public sealed class WebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly SallaWebhookProcessor _sallaProcessor;
        private readonly ZidWebhookProcessor _zidProcessor;
        private readonly WebhookSettings _settings;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IUnitOfWork unitOfWork,
            SallaWebhookProcessor sallaProcessor,
            ZidWebhookProcessor zidProcessor,
            IOptions<WebhookSettings> webhookOptions,
            ILogger<WebhookController> logger)
        {
            _unitOfWork = unitOfWork;
            _sallaProcessor = sallaProcessor;
            _zidProcessor = zidProcessor;
            _settings = webhookOptions.Value;
            _logger = logger;
        }

        // ── Salla ─────────────────────────────────────────────────────────────

        /// <summary>POST api/webhooks/salla</summary>
        [HttpPost("salla")]
        public async Task<IActionResult> Salla(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Salla-Signature");
            var eventType = GetHeader("X-Salla-Event") ?? "unknown";

            if (!VerifyHmac(rawBody, _settings.SallaSecret, signature))
            {
                _logger.LogWarning("[Webhook:Salla] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            _logger.LogInformation("[Webhook:Salla] Event received: {Event}", eventType);

            // ① احفظ الـ event في DB
            var webhookEvent = BuildWebhookEvent(rawBody, eventType);

            // استخرج الـ StoreIntegrationId من الـ payload لو موجود
            webhookEvent.StoreIntegrationId = ExtractStoreIntegrationId(rawBody);

            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();

            // ② شغّل الـ processor بالـ ID
            try
            {
                await _sallaProcessor.ProcessAsync(webhookEvent.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Webhook:Salla] Processing failed for event: {Event} Id: {Id}",
                    eventType, webhookEvent.Id);
            }

            return Ok(new { received = true });
        }

        // ── Zid ───────────────────────────────────────────────────────────────

        /// <summary>POST api/webhooks/zid</summary>
        [HttpPost("zid")]
        public async Task<IActionResult> Zid(CancellationToken ct)
        {
            var rawBody = await ReadRawBodyAsync();
            var signature = GetHeader("X-Zid-Signature");
            var eventType = GetHeader("X-Zid-Event") ?? "unknown";

            if (!VerifyHmac(rawBody, _settings.ZidSecret, signature))
            {
                _logger.LogWarning("[Webhook:Zid] Invalid signature — rejected");
                return Unauthorized(new { error = "Invalid signature" });
            }

            _logger.LogInformation("[Webhook:Zid] Event received: {Event}", eventType);

            // ① احفظ الـ event في DB
            var webhookEvent = BuildWebhookEvent(rawBody, eventType);

            webhookEvent.StoreIntegrationId = ExtractStoreIntegrationId(rawBody);

            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();

            // ② شغّل الـ processor بالـ ID
            try
            {
                await _zidProcessor.ProcessAsync(webhookEvent.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Webhook:Zid] Processing failed for event: {Event} Id: {Id}",
                    eventType, webhookEvent.Id);
            }

            return Ok(new { received = true });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static WebhookEvent BuildWebhookEvent(
            byte[] rawBody,
            string eventType) => new()
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
        /// بيحاول يستخرج الـ StoreIntegrationId من الـ payload.
        /// Salla/Zid بيبعتوا store_id — محتاج تربطه بالـ StoreIntegration المحلي.
        /// لو مش موجود يرجع Guid.Empty والـ processor يتعامل معاه.
        /// </summary>
        private static Guid ExtractStoreIntegrationId(byte[] rawBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                // بعض المنصات بتحط الـ store_id في data
                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("store_id", out var storeEl) &&
                    Guid.TryParse(storeEl.GetString(), out var id))
                    return id;

                // أو في root مباشرة
                if (root.TryGetProperty("store_id", out var rootStore) &&
                    Guid.TryParse(rootStore.GetString(), out var rootId))
                    return rootId;
            }
            catch { /* payload مش JSON صح */ }

            return Guid.Empty;
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

        /// <summary>
        /// HMAC-SHA256 verification بـ timing-safe comparison.
        /// بيدعم sha256= prefix (Salla style).
        /// </summary>
        private static bool VerifyHmac(byte[] body, string secret, string? receivedSignature)
        {
            if (string.IsNullOrWhiteSpace(receivedSignature) ||
                string.IsNullOrWhiteSpace(secret))
                return false;

            var sig = receivedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? receivedSignature[7..]
                : receivedSignature;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(sig.ToLowerInvariant()));
        }
    }
}