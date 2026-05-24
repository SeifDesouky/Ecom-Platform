using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.WhatsAppCatalog
{
    /// <summary>
    /// WhatsApp Business Catalog API (Meta Cloud API)
    /// Docs: https://developers.facebook.com/docs/whatsapp/cloud-api/reference/catalog
    /// Auth: WhatsApp Business API Token (System User Token)
    /// ملاحظة: WhatsApp Catalog = عرض منتجات فقط — الأوردرات عبر WhatsApp Messages
    /// نفس الـ Facebook Catalog لكن بيتعرض في WhatsApp Business
    /// </summary>
    public class WhatsAppCatalogAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://graph.facebook.com/v19.0";

        private readonly HttpClient _httpClient;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.WhatsAppCatalog;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = false,  // الأوردرات عبر WhatsApp Messages — مش API
            SupportsCustomers = false,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,   // بيستقبل message webhooks
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = true,
            SupportsRealTimeSync = false,
            SupportedSyncDirections =
            [
                SyncDirection.Export,      // من Fatora → WhatsApp فقط
            ],
            SupportedEntityTypes =
            [
                SyncEntityType.Products,
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public WhatsAppCatalogAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appSecret = configuration["WhatsAppCatalog:AppSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var catalogId = integration.ExternalStoreId;
                if (string.IsNullOrEmpty(catalogId))
                    return AdapterResult.Failure("Catalog ID (ExternalStoreId) is required");

                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/{catalogId}?fields=id,name,vertical", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Connection failed: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<WaIdResponse>(content, _json);
                return !string.IsNullOrEmpty(result?.Id)
                    ? AdapterResult.Success()
                    : AdapterResult.Failure("Invalid catalog response");
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
            // WhatsApp System User Tokens مش بتنتهي — مش محتاج refresh
            return await Task.FromResult(
                AdapterResult<TokenData>.Failure(
                    "WhatsApp System User tokens do not expire. Re-generate from Meta Business Manager if needed.",
                    "NOT_SUPPORTED"));
        }

        // ── Products (Catalog Items) ──────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var catalogId = integration.ExternalStoreId;
                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl}/{catalogId}/products" +
                                $"?limit={limit}" +
                                $"&fields=id,retailer_id,name,description,price,currency,availability,inventory,image_url";

                if (!string.IsNullOrEmpty(filter?.Cursor))
                    url += $"&after={filter.Cursor}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<WaListResponse<WaProduct>>(content, _json);
                var products = root?.Data?.Select(MapToExternalProduct).ToList()
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

                var url = $"{BaseUrl}/{externalId}" +
                               $"?fields=id,retailer_id,name,description,price,currency,availability,inventory,image_url";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<WaProduct>(content, _json);
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

                var catalogId = integration.ExternalStoreId;
                var url = $"{BaseUrl}/{catalogId}/products";

                var body = new
                {
                    retailer_id = product.Sku ?? Guid.NewGuid().ToString(),
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    price = (int)(product.Price * 100), // cents
                    currency = "SAR",
                    availability = product.StockQuantity > 0 ? "in stock" : "out of stock",
                    inventory = product.StockQuantity,
                    image_url = product.ImageUrl ?? string.Empty,
                    url = product.ImageUrl ?? string.Empty,
                    condition = "new"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<WaIdResponse>(content, _json);
                if (string.IsNullOrEmpty(result?.Id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(result.Id);
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

                var body = new
                {
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    price = (int)(product.Price * 100),
                    currency = "SAR",
                    availability = product.StockQuantity > 0 ? "in stock" : "out of stock",
                    inventory = product.StockQuantity,
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync( // Meta بيستخدم POST للـ update
                    $"{BaseUrl}/{product.ExternalId}", request, ct);

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

                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/{externalId}", ct);

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
                "WhatsApp Catalog does not support orders API. Orders are managed via WhatsApp messages.",
                "NOT_SUPPORTED"));

        public Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<ExternalOrder>.Failure(
                "WhatsApp Catalog does not support orders API.", "NOT_SUPPORTED"));

        public Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(
                "WhatsApp Catalog does not support orders API.", "NOT_SUPPORTED"));

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

                // Meta Batch API
                var batchRequests = items.Select(item => new
                {
                    method = "POST",
                    relative_url = item.ExternalProductId,
                    body = $"inventory={item.Quantity}&availability={(item.Quantity > 0 ? "in+stock" : "out+of+stock")}"
                }).ToList();

                var batchBody = new { batch = batchRequests };
                var json = JsonSerializer.Serialize(batchBody, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{BaseUrl}/", request, ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update inventory: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks ─────────────────────────────────────────────────────────

        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // WhatsApp Webhooks — بتتسجل على الـ Phone Number أو الـ App
                var phoneNumberId = integration.ExternalStoreId;
                var url = $"{BaseUrl}/{phoneNumberId}/subscribed_apps";
                var response = await _httpClient.PostAsync(
                    url, new StringContent("{}", Encoding.UTF8, "application/json"), ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to register webhooks: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var phoneNumberId = integration.ExternalStoreId;
                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/{phoneNumberId}/subscribed_apps", ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to unregister webhooks: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
        {
            if (string.IsNullOrEmpty(_appSecret)) return false;

            // نفس Meta signature verification
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey ?? string.Empty);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(WaProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.RetailerId,
            Price = p.Price / 100m,
            StockQuantity = p.Inventory ?? 0,
            IsActive = p.Availability == "in stock",
            ImageUrl = p.ImageUrl,
        };
    }

    // ── WhatsApp / Meta API Models ────────────────────────────────────────────

    internal class WaIdResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    internal class WaListResponse<T>
    {
        public List<T>? Data { get; set; }
        public WaPaging? Paging { get; set; }
    }

    internal class WaPaging
    {
        public WaCursors? Cursors { get; set; }
        public string? Next { get; set; }
    }

    internal class WaCursors
    {
        public string? Before { get; set; }
        public string? After { get; set; }
    }

    internal class WaProduct
    {
        public string? Id { get; set; }
        public string? RetailerId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? Availability { get; set; }
        public int? Inventory { get; set; }
        public string? ImageUrl { get; set; }
    }
}