using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Core.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Salla
{
    public class SallaAuthService
    {
        private const string TokenUrl = "https://accounts.salla.sa/oauth2/token";

        private readonly HttpClient _httpClient;

        public SallaAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ── Code Exchange (OAuth callback) ───────────────────────────────────

        /// <summary>
        /// يبادل authorization code بـ access + refresh tokens.
        /// يُستدعى مرة واحدة فقط عند أول ربط.
        /// </summary>
        public async Task<AdapterResult<TokenData>> ExchangeCodeAsync(
            string code,
            string clientId,
            string clientSecret,
            string redirectUri,
            CancellationToken ct = default)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
            });

            try
            {
                var response = await _httpClient.PostAsync(TokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Code exchange failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<SallaTokenResponse>(content);
                if (token?.AccessToken is null or "")
                    return AdapterResult<TokenData>.Failure("Empty access token in response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
                    TokenType = token.TokenType
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Code exchange error: {ex.Message}");
            }
        }

        // ── Refresh Token ────────────────────────────────────────────────────

        /// <summary>
        /// يجدد الـ access token باستخدام الـ refresh token.
        /// الـ integration يجي بـ tokens مفكوك تشفيرها من IntegrationService.
        /// </summary>
        public async Task<AdapterResult<TokenData>> RefreshAccessTokenAsync(
            StoreIntegration integration,
            string clientId,
            string clientSecret,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(integration.RefreshToken))
                return AdapterResult<TokenData>.Failure("No refresh token available");

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = integration.RefreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            });

            try
            {
                var response = await _httpClient.PostAsync(TokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<SallaTokenResponse>(content);
                if (token?.AccessToken is null or "")
                    return AdapterResult<TokenData>.Failure("Empty access token in response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
                    TokenType = token.TokenType
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Token refresh error: {ex.Message}");
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private sealed class SallaTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; init; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; init; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; init; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; init; } = string.Empty;
        }
    }
}