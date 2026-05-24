using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Etsy
{
    /// <summary>
    /// Etsy REST API Adapter
    /// Auth: OAuth2 — Authorization Code Grant (PKCE)
    /// Docs: https://developers.etsy.com/documentation
    /// APIs used: Etsy Open API v3
    /// </summary>
    public class EtsyAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://openapi.etsy.com/v3";
        private const string TokenUrl = "https://api.etsy.com/v3/public/oauth/token";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Etsy;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = false, // Etsy لا يدعم Webhooks بشكل رسمي
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = false,
            SupportsRealTimeSync = false,
            SupportedSyncDirections =
            [
                SyncDirection.Import,
                SyncDirection.Export,
                SyncDirection.BiDirectional
            ],
            SupportedEntityTypes =
            [
                SyncEntityType.Products,
                SyncEntityType.Orders,
                SyncEntityType.Customers,
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public EtsyAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Etsy:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Etsy:ClientSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/application/openapi-ping", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid or expired OAuth token", "UNAUTHORIZED", 401);

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
                    ["client_id"] = _clientId,
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty
                });

                var response = await _httpClient.PostAsync(TokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<EtsyTokenResponse>(content, _json);
                if (token is null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken ?? integration.RefreshToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products (Listings) ──────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var shopId = integration.ExternalStoreId ?? string.Empty;
                var limit = filter?.PageSize ?? 100; // Etsy max = 100
                var offset = ((filter?.Page ?? 1) - 1) * limit;
                var allProducts = new List<ExternalProduct>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{BaseUrl}/application/shops/{shopId}/listings/active?limit={limit}&offset={offset}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var etsyResponse = JsonSerializer.Deserialize<EtsyListingsResponse>(content, _json);
                    if (etsyResponse?.Results is not null)
                        allProducts.AddRange(etsyResponse.Results.Select(MapToExternalProduct));

                    var total = etsyResponse?.Count ?? 0;
                    offset += limit;
                    hasMore = offset < total && (filter == null || filter.Page == 0);
                }

                return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(allProducts);
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

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/application/listings/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var listing = JsonSerializer.Deserialize<EtsyListing>(content, _json);
                if (listing is null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(listing));
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

                var shopId = integration.ExternalStoreId ?? string.Empty;

                var body = new
                {
                    quantity = product.StockQuantity,
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    who_made = "i_did",
                    when_made = "made_to_order",
                    taxonomy_id = 1, // يُحدَّث حسب الفئة
                    type = "physical",
                    state = product.IsActive ? "active" : "draft"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/application/shops/{shopId}/listings", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create listing: {content}",
                        statusCode: (int)response.StatusCode);

                var listing = JsonSerializer.Deserialize<EtsyListing>(content, _json);
                var listingId = listing?.ListingId.ToString();

                if (string.IsNullOrEmpty(listingId))
                    return AdapterResult<string>.Failure("Listing created but ID not returned");

                return AdapterResult<string>.Success(listingId);
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
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    quantity = product.StockQuantity,
                    state = product.IsActive ? "active" : "inactive"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{BaseUrl}/application/listings/{product.ExternalId}", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to update listing: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
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

                // Etsy: حذف listing = تغيير state إلى deleted
                var body = new { state = "deleted" };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{BaseUrl}/application/listings/{externalId}", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to delete listing: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders (Receipts) ────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var shopId = integration.ExternalStoreId ?? string.Empty;
                var limit = filter?.PageSize ?? 100;
                var offset = ((filter?.Page ?? 1) - 1) * limit;
                var allOrders = new List<ExternalOrder>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{BaseUrl}/application/shops/{shopId}/receipts?limit={limit}&offset={offset}";

                    if (filter?.ModifiedAfter != null)
                    {
                        var unixTime = ((DateTimeOffset)filter.ModifiedAfter).ToUnixTimeSeconds();
                        url += $"&min_last_modified={unixTime}";
                    }

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var etsyResponse = JsonSerializer.Deserialize<EtsyReceiptsResponse>(content, _json);
                    if (etsyResponse?.Results is not null)
                        allOrders.AddRange(etsyResponse.Results.Select(MapToExternalOrder));

                    var total = etsyResponse?.Count ?? 0;
                    offset += limit;
                    hasMore = offset < total && (filter == null || filter.Page == 0);
                }

                return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(allOrders);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var shopId = integration.ExternalStoreId ?? string.Empty;

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/application/shops/{shopId}/receipts/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var receipt = JsonSerializer.Deserialize<EtsyReceipt>(content, _json);
                if (receipt is null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(receipt));
            }
            catch (Exception ex)
            {
                return AdapterResult<ExternalOrder>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var shopId = integration.ExternalStoreId ?? string.Empty;

                // Etsy: تحديث الطلب = إنشاء shipment tracking
                if (newStatus.ToLower() == "shipped")
                {
                    var body = new
                    {
                        tracking_code = string.Empty,
                        carrier_name = string.Empty,
                        send_bcc = false
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/application/shops/{shopId}/receipts/{externalId}/tracking",
                        request, ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult.Failure(
                            $"Failed to update order status: {content}",
                            statusCode: (int)response.StatusCode);
                }

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

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
                })
                .ToList() ?? [];

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

                var errors = new List<string>();

                foreach (var item in items)
                {
                    // Etsy: تحديث الكمية عبر listing inventory endpoint
                    var body = new
                    {
                        products = new[]
                        {
                            new
                            {
                                sku = item.Sku ?? item.ExternalProductId,
                                offerings = new[]
                                {
                                    new { quantity = item.Quantity, is_enabled = true }
                                }
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PutAsync(
                        $"{BaseUrl}/application/listings/{item.ExternalProductId}/inventory",
                        request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Listing {item.ExternalProductId}: {content}");
                    }
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Some inventory updates failed: {string.Join(" | ", errors)}")
                    : AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks ─────────────────────────────────────────────────────────
        // Etsy لا يدعم Webhooks — polling فقط

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Failure(
                "Etsy does not support webhooks. Use polling instead.", "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Success());

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
            => false; // Etsy لا يدعم Webhooks

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);
            _httpClient.DefaultRequestHeaders.Remove("x-api-key");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _clientId);
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(EtsyListing l) => new()
        {
            ExternalId = l.ListingId.ToString(),
            Name = l.Title ?? string.Empty,
            Description = l.Description,
            Sku = l.Sku ?? l.ListingId.ToString(),
            Price = l.Price?.Amount > 0
                ? (decimal)l.Price.Amount / (l.Price.Divisor > 0 ? l.Price.Divisor : 1)
                : 0,
            StockQuantity = l.Quantity,
            IsActive = l.State == "active",
            ImageUrl = l.Images?.FirstOrDefault()?.UrlFullxfull,
            Categories = l.Tags ?? [],
            Variants = [],
            UpdatedAt = l.LastModifiedTsz > 0
                ? DateTimeOffset.FromUnixTimeSeconds(l.LastModifiedTsz).UtcDateTime
                : null
        };

        private static ExternalOrder MapToExternalOrder(EtsyReceipt r) => new()
        {
            ExternalId = r.ReceiptId.ToString(),
            OrderNumber = r.ReceiptId.ToString(),
            Status = MapFromEtsyStatus(r.Status ?? string.Empty),
            TotalAmount = r.GrandTotal?.Amount > 0
                ? (decimal)r.GrandTotal.Amount / (r.GrandTotal.Divisor > 0 ? r.GrandTotal.Divisor : 1)
                : 0,
            Currency = r.GrandTotal?.CurrencyCode ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = r.BuyerUserId.ToString(),
                Name = r.Name ?? string.Empty,
                Email = r.BuyerEmail ?? string.Empty
            },
            Items = r.Transactions?.Select(t => new ExternalOrderItem
            {
                ExternalProductId = t.ListingId.ToString(),
                ProductName = t.Title ?? string.Empty,
                Sku = t.Sku ?? string.Empty,
                Quantity = t.Quantity,
                UnitPrice = t.Price?.Amount > 0
                    ? (decimal)t.Price.Amount / (t.Price.Divisor > 0 ? t.Price.Divisor : 1)
                    : 0,
                TotalPrice = t.Price?.Amount > 0
                    ? (decimal)t.Price.Amount / (t.Price.Divisor > 0 ? t.Price.Divisor : 1) * t.Quantity
                    : 0
            }).ToList() ?? [],
            ShippingAddress = r.FirstLine is null ? null : new ExternalAddress
            {
                Street = r.FirstLine,
                City = r.City,
                Country = r.CountryIso,
                PostalCode = r.Zip
            },
            CreatedAt = r.CreateTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(r.CreateTimestamp).UtcDateTime
                : DateTime.UtcNow,
            UpdatedAt = r.UpdateTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(r.UpdateTimestamp).UtcDateTime
                : null
        };

        private static string MapFromEtsyStatus(string status) =>
            status.ToLower() switch
            {
                "open" => "pending",
                "paid" => "processing",
                "completed" => "delivered",
                "cancelled" => "cancelled",
                _ => status.ToLower()
            };
    }

    // ── Etsy API Models ───────────────────────────────────────────────────────

    internal class EtsyTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? TokenType { get; set; }
    }

    // — Listings —
    internal class EtsyListingsResponse
    {
        public List<EtsyListing>? Results { get; set; }
        public int Count { get; set; }
    }

    internal class EtsyListing
    {
        public long ListingId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public string? State { get; set; }
        public int Quantity { get; set; }
        public EtsyPrice? Price { get; set; }
        public List<EtsyImage>? Images { get; set; }
        public List<string>? Tags { get; set; }
        public long LastModifiedTsz { get; set; }
    }

    internal class EtsyPrice
    {
        public long Amount { get; set; }
        public long Divisor { get; set; }
        public string? CurrencyCode { get; set; }
    }

    internal class EtsyImage
    {
        public long ListingImageId { get; set; }
        public string? UrlFullxfull { get; set; }
        public string? Url570xN { get; set; }
    }

    // — Receipts (Orders) —
    internal class EtsyReceiptsResponse
    {
        public List<EtsyReceipt>? Results { get; set; }
        public int Count { get; set; }
    }

    internal class EtsyReceipt
    {
        public long ReceiptId { get; set; }
        public long BuyerUserId { get; set; }
        public string? BuyerEmail { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public EtsyPrice? GrandTotal { get; set; }
        public List<EtsyTransaction>? Transactions { get; set; }
        public string? FirstLine { get; set; }
        public string? City { get; set; }
        public string? CountryIso { get; set; }
        public string? Zip { get; set; }
        public long CreateTimestamp { get; set; }
        public long UpdateTimestamp { get; set; }
    }

    internal class EtsyTransaction
    {
        public long TransactionId { get; set; }
        public long ListingId { get; set; }
        public string? Title { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public EtsyPrice? Price { get; set; }
    }
}