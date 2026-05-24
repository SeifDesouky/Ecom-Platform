using Asp.Versioning;
using EcomPlatform.Application.Common;
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Infrastructure.Adapters.Salla;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomPlatform.API.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/integrations/oauth")]
    public class OAuthController : ControllerBase
    {
        private readonly SallaOAuthService _sallaOAuth;
        private readonly ITenantProvider _tenantProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuthController> _logger;

        public OAuthController(
            SallaOAuthService sallaOAuth,
            ITenantProvider tenantProvider,
            IConfiguration configuration,
            ILogger<OAuthController> logger)
        {
            _sallaOAuth = sallaOAuth;
            _tenantProvider = tenantProvider;
            _configuration = configuration;
            _logger = logger;
        }

        // ── Step 1: Redirect التاجر لسلة ────────────────────────────────────

        /// <summary>
        /// يولد Authorization URL ويعمل redirect للتاجر.
        /// التاجر لازم يكون عنده integration مسبقًا (created بـ PendingSetup status).
        /// GET /api/v1/integrations/oauth/salla/authorize/{integrationId}
        /// </summary>
        [Authorize]
        [HttpGet("salla/authorize/{integrationId:guid}")]
        public async Task<IActionResult> SallaAuthorize(
            Guid integrationId,
            CancellationToken ct)
        {
            try
            {
                var authUrl = await _sallaOAuth.GenerateAuthorizationUrlAsync(integrationId, ct);
                return Redirect(authUrl);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate Salla auth URL for {Id}", integrationId);
                return StatusCode(500, ApiResponse<string>.Fail("Failed to initiate OAuth flow"));
            }
        }

        // ── Step 2: Callback من سلة ──────────────────────────────────────────

        /// <summary>
        /// سلة بترجع هنا بعد موافقة التاجر.
        /// بيعمل code exchange وبيحفظ الـ tokens.
        /// GET /api/v1/integrations/oauth/salla/callback?code=xxx&state=yyy
        /// </summary>
        [AllowAnonymous]   // سلة بترسل request مباشرة — مش في JWT context
        [HttpGet("salla/callback")]
        public async Task<IActionResult> SallaCallback(
            [FromQuery] string code,
            [FromQuery] string state,
            [FromQuery] string? error,
            [FromQuery] string? error_description,
            CancellationToken ct)
        {
            var frontendBase = _configuration["FrontendBaseUrl"] ?? "https://rahtk.sa";

            // التاجر رفض الـ permissions
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Salla OAuth denied: {Error} — {Desc}", error, error_description);
                return Redirect($"{frontendBase}/integrations?oauth=denied&reason={Uri.EscapeDataString(error_description ?? error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Redirect($"{frontendBase}/integrations?oauth=error&reason=missing_params");

            var result = await _sallaOAuth.HandleCallbackAsync(code, state, ct);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Salla OAuth callback failed: {Error}", result.ErrorMessage);
                return Redirect($"{frontendBase}/integrations?oauth=error&reason={Uri.EscapeDataString(result.ErrorMessage ?? "unknown")}");
            }

            // نجح — نرجع التاجر للداشبورد مع رسالة نجاح
            return Redirect($"{frontendBase}/integrations/{result.IntegrationId}?oauth=success");
        }
    }
}