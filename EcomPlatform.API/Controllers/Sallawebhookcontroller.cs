using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters.Salla;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace EcomPlatform.API.Controllers.V1
{
    /// <summary>
    /// يستقبل Webhook events من سلة.
    /// AllowAnonymous — سلة بتبعت requests مباشرة بدون JWT.
    /// التحقق بيتم عن طريق HMAC-SHA256 signature.
    /// POST /api/v1/webhooks/salla/{integrationId}
    /// </summary>
    [ApiController]
    [Route("api/v1/webhooks/salla")]
    public class SallaWebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEncryptionService _encryption;
        private readonly SallaWebhookProcessor _processor;
        private readonly ILogger<SallaWebhookController> _logger;

        public SallaWebhookController(
            IUnitOfWork unitOfWork,
            IEncryptionService encryption,
            SallaWebhookProcessor processor,
            ILogger<SallaWebhookController> logger)
        {
            _unitOfWork = unitOfWork;
            _encryption = encryption;
            _processor = processor;
            _logger = logger;
        }

        [HttpPost("{integrationId:guid}")]
        public async Task<IActionResult> Receive(
            Guid integrationId,
            CancellationToken ct)
        {
            // ── 1. قراءة الـ payload الخام ────────────────────────────────
            string rawPayload;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                rawPayload = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(rawPayload))
                return BadRequest("Empty payload");

            // ── 2. جلب الـ integration ────────────────────────────────────
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration == null || integration.IsDeleted)
            {
                _logger.LogWarning("Webhook received for unknown integration {Id}", integrationId);
                return NotFound();
            }

            // ── 3. التحقق من الـ Signature (HMAC-SHA256) ─────────────────
            var signature = Request.Headers["X-Salla-Signature"].FirstOrDefault() ?? string.Empty;
            var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var eventType = Request.Headers["X-Salla-Event"].FirstOrDefault() ?? "unknown";
            var isVerified = false;

            var webhookSecret = _encryption.Decrypt(integration.WebhookSecret);

            if (!string.IsNullOrEmpty(webhookSecret))
            {
                isVerified = VerifyHmacSignature(rawPayload, signature, webhookSecret);

                if (!isVerified)
                {
                    _logger.LogWarning(
                        "Invalid webhook signature for integration {Id} — event {Event}",
                        integrationId, eventType);

                    // نحفظ الـ event لكن مش نعالجه
                    await SaveWebhookEventAsync(
                        integrationId, integration.TenantId, eventType,
                        rawPayload, signature, sourceIp,
                        isVerified: false,
                        status: WebhookEventStatus.Failed,
                        error: "Invalid signature",
                        ct);

                    return Unauthorized("Invalid signature");
                }
            }
            else
            {
                // لو مفيش WebhookSecret — نقبل لكن نسجل تحذير
                _logger.LogWarning(
                    "No webhook secret configured for integration {Id} — skipping signature verification",
                    integrationId);
            }

            // ── 4. حفظ الـ event في DB ────────────────────────────────────
            var webhookEvent = await SaveWebhookEventAsync(
                integrationId, integration.TenantId, eventType,
                rawPayload, signature, sourceIp,
                isVerified: isVerified,
                status: WebhookEventStatus.Received,
                error: null,
                ct);

            // ── 5. نرد على سلة فورًا بـ 200 ─────────────────────────────
            // المعالجة الفعلية تحصل في الـ background عشان مانخليش سلة تستنى
            _ = Task.Run(async () =>
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<SallaWebhookProcessor>();
                await processor.ProcessAsync(webhookEvent.Id, CancellationToken.None);
            }, CancellationToken.None);

            return Ok(new { received = true, eventId = webhookEvent.Id });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool VerifyHmacSignature(string payload, string signature, string secret)
        {
            if (string.IsNullOrEmpty(signature))
                return false;

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(secret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            // Constant-time comparison لمنع timing attacks
            return CryptographicEquals(expected, signature.ToLower());
        }

        private static bool CryptographicEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var result = 0;
            for (var i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }

        private async Task<WebhookEvent> SaveWebhookEventAsync(
            Guid integrationId,
            Guid? tenantId,
            string eventType,
            string rawPayload,
            string signature,
            string? sourceIp,
            bool isVerified,
            WebhookEventStatus status,
            string? error,
            CancellationToken ct)
        {
            var webhookEvent = new WebhookEvent
            {
                StoreIntegrationId = integrationId,
                TenantId = tenantId,
                EventType = eventType,
                RawPayload = rawPayload,
                Signature = signature,
                SourceIp = sourceIp,
                IsVerified = isVerified,
                Status = status,
                ErrorMessage = error,
                LastAttemptAt = DateTime.UtcNow
            };

            await _unitOfWork.WebhookEvents.AddAsync(webhookEvent);
            await _unitOfWork.SaveChangesAsync();

            return webhookEvent;
        }
    }
}