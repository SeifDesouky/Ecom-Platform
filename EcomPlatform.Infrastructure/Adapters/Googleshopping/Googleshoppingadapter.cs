using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.GoogleShopping
{
    /// <summary>
    /// Google Merchant Center Content API v2.1
    /// Docs: https://developers.google.com/shopping-content/reference/rest
    /// Auth: OAuth2 Service Account أو OAuth2 User
    /// ملاحظة: Google Shopping = عرض المنتجات فقط — مفيش orders API
    /// الأوردرات بتيجي عبر Google Ads أو Google Shopping Actions (محدود)
    /// </summary>
    public class GoogleShoppingAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://shoppingcontent.googleapis.com/content/v2.1";
        private const string TokenUrl = "https://oauth2.googleapis.com/token";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.GoogleShopping;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = false, // Google Shopping مش بيدعم orders API
            SupportsCustomers = false,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = false, // بيستخدم Pub/Sub مش webhooks تقليدي
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = true,  // بيدعم batch operations
            SupportsRealTimeSync = false,
            SupportedSyncDirections =
            [
                SyncDirection.Export,     // من Fatora → Google فقط
            ],
            SupportedEntityTypes =
            [
                SyncEntityType.Products,
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public GoogleShoppingAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["GoogleShopping:ClientId"] ?? string.Empty;
            _clientSecret = configuration["GoogleShopping:ClientSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;
                if (string.IsNullOrEmpty(merchantId))
                    return AdapterResult.Failure("Merchant ID (ExternalStoreId) is required", "MISSING_MERCHANT_ID");

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/{merchantId}/accounts/{merchantId}", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {response.StatusCode}",
                    statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Connection error: {ex.Message}");
            }
        }

        public async Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var response = await _httpClient.PostAsync(TokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<GoogleTokenResponse>(content, _json);
                if (token == null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = integration.RefreshToken ?? string.Empty, // Google مش بيبعت refresh token جديد
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products ─────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;
                var maxResults = filter?.PageSize ?? 50;
                var url = $"{BaseUrl}/{merchantId}/products?maxResults={maxResults}";

                if (!string.IsNullOrEmpty(filter?.Cursor))
                    url += $"&pageToken={filter.Cursor}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<GoogleProductsResponse>(content, _json);
                var products = root?.Resources?.Select(MapToExternalProduct).ToList()
                    ?? new List<ExternalProduct>();

                return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(products);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/{merchantId}/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<GoogleProduct>(content, _json);
                if (product == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(product));
            }
            catch (Exception ex)
            {
                return AdapterResult<ExternalProduct>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult<string>> CreateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;

                // Google Shopping محتاج channel + contentLanguage + targetCountry + offerId
                var googleProduct = new GoogleProduct
                {
                    OfferId = product.Sku ?? product.ExternalId ?? Guid.NewGuid().ToString(),
                    Title = product.Name,
                    Description = product.Description ?? string.Empty,
                    Link = product.ImageUrl ?? string.Empty,
                    ImageLink = product.ImageUrl ?? string.Empty,
                    Channel = "online",
                    ContentLanguage = "ar",
                    TargetCountry = "SA",
                    Availability = product.StockQuantity > 0 ? "in stock" : "out of stock",
                    Condition = "new",
                    Price = new GooglePrice
                    {
                        Value = product.Price.ToString("F2"),
                        Currency = "SAR"
                    }
                };

                var json = JsonSerializer.Serialize(googleProduct, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/{merchantId}/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<GoogleProduct>(content, _json);
                if (string.IsNullOrEmpty(created?.Id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(created.Id);
            }
            catch (Exception ex)
            {
                return AdapterResult<string>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult> UpdateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;
                var googleProduct = new GoogleProduct
                {
                    OfferId = product.Sku ?? product.ExternalId ?? string.Empty,
                    Title = product.Name,
                    Description = product.Description ?? string.Empty,
                    Channel = "online",
                    ContentLanguage = "ar",
                    TargetCountry = "SA",
                    Availability = product.StockQuantity > 0 ? "in stock" : "out of stock",
                    Price = new GooglePrice
                    {
                        Value = product.Price.ToString("F2"),
                        Currency = "SAR"
                    }
                };

                var json = JsonSerializer.Serialize(googleProduct, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                // Google بيستخدم PUT بالـ full product ID
                var response = await _httpClient.PutAsync(
                    $"{BaseUrl}/{merchantId}/products/{product.ExternalId}", request, ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update product: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;
                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/{merchantId}/products/{externalId}", ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to delete product: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders — غير مدعوم ───────────────────────────────────────────────

        public Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                "Google Shopping does not support orders API", "NOT_SUPPORTED"));

        public Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<ExternalOrder>.Failure(
                "Google Shopping does not support orders API", "NOT_SUPPORTED"));

        public Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(
                "Google Shopping does not support orders API", "NOT_SUPPORTED"));

        // ── Inventory ────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            var productsResult = await GetProductsAsync(integration, ct: ct);
            if (!productsResult.IsSuccess)
                return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                    productsResult.ErrorMessage ?? "Failed to get inventory");

            var inventory = productsResult.Data?
                .Select(p => new ExternalInventory
                {
                    ExternalProductId = p.ExternalId,
                    Sku = p.Sku,
                    Quantity = p.StockQuantity
                }).ToList() ?? [];

            return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(inventory);
        }

        public async Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var merchantId = integration.ExternalStoreId;

                // Google بيستخدم inventory endpoint لتحديث الـ stock فقط
                var errors = new List<string>();

                foreach (var item in items)
                {
                    var body = new
                    {
                        availability = item.Quantity > 0 ? "in stock" : "out of stock",
                        quantity = item.Quantity,
                        price = (object?)null  // مش بنحدث السعر هنا
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/{merchantId}/inventory/{merchantId}/stores/online/products/{item.ExternalProductId}",
                        request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Product {item.ExternalProductId}: {content}");
                    }
                }

                return errors.Count == 0
                    ? AdapterResult.Success()
                    : AdapterResult.Failure($"Some inventory updates failed: {string.Join(" | ", errors)}");
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks — غير مدعوم (بيستخدم Google Pub/Sub) ──────────────────

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(
                "Google Shopping uses Pub/Sub notifications, not webhooks. Configure via Google Cloud Console.",
                "NOT_SUPPORTED"));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(
                "Google Shopping uses Pub/Sub notifications, not webhooks.",
                "NOT_SUPPORTED"));

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature) => false; // Google مش بيستخدم webhook signatures

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey ?? string.Empty);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(GoogleProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Title ?? string.Empty,
            Description = p.Description,
            Sku = p.OfferId,
            Price = decimal.TryParse(p.Price?.Value, out var price) ? price : 0,
            StockQuantity = p.Availability == "in stock" ? 1 : 0,
            IsActive = p.Availability == "in stock",
            ImageUrl = p.ImageLink,
        };
    }

    // ── Google API Models ─────────────────────────────────────────────────────

    internal class GoogleTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }

    internal class GoogleProductsResponse
    {
        public string? NextPageToken { get; set; }
        public List<GoogleProduct>? Resources { get; set; }
    }

    internal class GoogleProduct
    {
        public string? Id { get; set; }
        public string? OfferId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public string? ImageLink { get; set; }
        public string? Channel { get; set; }
        public string? ContentLanguage { get; set; }
        public string? TargetCountry { get; set; }
        public string? Availability { get; set; }
        public string? Condition { get; set; }
        public GooglePrice? Price { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }

    internal class GooglePrice
    {
        public string? Value { get; set; }
        public string? Currency { get; set; }
    }
}