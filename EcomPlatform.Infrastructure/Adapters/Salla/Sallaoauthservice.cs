using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EcomPlatform.Infrastructure.Adapters.Salla
{
    /// <summary>
    /// يدير OAuth 2.0 flow كامل مع سلة:
    ///   1. يولد Authorization URL ويحفظ state في DB
    ///   2. يستقبل callback ويعمل code exchange
    ///   3. يحفظ الـ tokens مشفرة في StoreIntegration
    /// </summary>
    public class SallaOAuthService
    {
        private const string AuthUrl = "https://accounts.salla.sa/oauth2/auth";
        private const string TokenUrl = "https://accounts.salla.sa/oauth2/token";

        private readonly SallaAuthService _authService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEncryptionService _encryption;
        private readonly ILogger<SallaOAuthService> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;

        public SallaOAuthService(
            SallaAuthService authService,
            IUnitOfWork unitOfWork,
            IEncryptionService encryption,
            IConfiguration configuration,
            ILogger<SallaOAuthService> logger)
        {
            _authService = authService;
            _unitOfWork = unitOfWork;
            _encryption = encryption;
            _logger = logger;
            _clientId = configuration["Salla:ClientId"] ?? throw new InvalidOperationException("Salla:ClientId missing");
            _clientSecret = configuration["Salla:ClientSecret"] ?? throw new InvalidOperationException("Salla:ClientSecret missing");
            _redirectUri = configuration["Salla:RedirectUri"] ?? throw new InvalidOperationException("Salla:RedirectUri missing");
        }

        // ── Step 1: توليد Authorization URL ─────────────────────────────────

        /// <summary>
        /// يولد URL يتوجه إليه التاجر عشان يوافق على الـ permissions.
        /// بيحفظ state في الـ integration كـ WebhookSecret مؤقتًا للتحقق في الـ callback.
        /// </summary>
        public async Task<string> GenerateAuthorizationUrlAsync(
            Guid integrationId,
            CancellationToken ct = default)
        {
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId)
                ?? throw new InvalidOperationException("Integration not found");

            // state = random token نتحقق منه في الـ callback لمنع CSRF
            var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            // نحفظ الـ state مشفر في الـ DB مؤقتًا
            integration.WebhookSecret = _encryption.Encrypt(state);
            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            var scopes = "offline_access,store.info,products.read,orders.read,customers.read";

            var queryParams = new Dictionary<string, string>
            {
                ["response_type"] = "code",
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri,
                ["scope"] = scopes,
                ["state"] = $"{integrationId}:{state}"   // integrationId مدمج في الـ state
            };

            var queryString = string.Join("&",
                queryParams.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            return $"{AuthUrl}?{queryString}";
        }

        // ── Step 2: استقبال Callback وتبادل الـ Code ─────────────────────────

        /// <summary>
        /// يستقبل code وstate من سلة، يتحقق من الـ state، يعمل token exchange،
        /// ويحفظ الـ tokens مشفرة في الـ StoreIntegration.
        /// </summary>
        public async Task<OAuthCallbackResult> HandleCallbackAsync(
            string code,
            string state,
            CancellationToken ct = default)
        {
            // استخراج integrationId من الـ state
            var parts = state.Split(':');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var integrationId))
                return OAuthCallbackResult.Fail("Invalid state format");

            var receivedState = parts[1];

            // جلب الـ integration
            var integration = await _unitOfWork.StoreIntegrations.GetByIdAsync(integrationId);
            if (integration == null || integration.IsDeleted)
                return OAuthCallbackResult.Fail("Integration not found");

            // التحقق من الـ state لمنع CSRF
            var savedState = _encryption.Decrypt(integration.WebhookSecret);
            if (savedState != receivedState)
            {
                _logger.LogWarning("OAuth state mismatch for integration {Id}", integrationId);
                return OAuthCallbackResult.Fail("State mismatch — possible CSRF attack");
            }

            // تبادل الـ code بـ tokens
            var tokenResult = await _authService.ExchangeCodeAsync(
                code, _clientId, _clientSecret, _redirectUri, ct);

            if (!tokenResult.IsSuccess)
                return OAuthCallbackResult.Fail(tokenResult.ErrorMessage ?? "Token exchange failed");

            var tokens = tokenResult.Data!;

            // حفظ الـ tokens مشفرة
            integration.ApiKey = _encryption.Encrypt(tokens.AccessToken);   // 🔒
            integration.RefreshToken = _encryption.Encrypt(tokens.RefreshToken);  // 🔒
            integration.TokenExpiresAt = tokens.ExpiresAt;
            integration.WebhookSecret = null;   // نمسح الـ state المؤقت
            integration.Status = Core.Enums.IntegrationStatus.Active;
            integration.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.StoreIntegrations.UpdateAsync(integration);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("OAuth completed for integration {Id}", integrationId);
            return OAuthCallbackResult.Ok(integrationId);
        }
    }

    // ── Result DTO ───────────────────────────────────────────────────────────

    public sealed class OAuthCallbackResult
    {
        public bool IsSuccess { get; private init; }
        public string? ErrorMessage { get; private init; }
        public Guid IntegrationId { get; private init; }

        public static OAuthCallbackResult Ok(Guid id) => new() { IsSuccess = true, IntegrationId = id };
        public static OAuthCallbackResult Fail(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }
}