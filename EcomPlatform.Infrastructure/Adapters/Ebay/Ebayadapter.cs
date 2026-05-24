using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.eBay
{
    /// <summary>
    /// eBay REST API Adapter
    /// Auth: OAuth2 — Authorization Code Grant
    /// Docs: https://developer.ebay.com/develop/apis/restful-apis
    /// APIs used: Inventory API, Fulfillment API, Taxonomy API
    /// </summary>
    public class EbayAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://api.ebay.com";
        private const string SandboxUrl = "https://api.sandbox.ebay.com";
        private const string TokenUrl = "https://api.ebay.com/identity/v1/oauth2/token";
        private const string MarketplaceId = "EBAY_US";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly bool _isSandbox;

        private string ApiBase => _isSandbox ? SandboxUrl : BaseUrl;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Ebay;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = true,
            SupportsRealTimeSync = true,
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

        public EbayAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["eBay:ClientId"] ?? string.Empty;
            _clientSecret = configuration["eBay:ClientSecret"] ?? string.Empty;
            _isSandbox = bool.TryParse(configuration["eBay:Sandbox"], out var sb) && sb;
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
                    $"{ApiBase}/sell/inventory/v1/location?limit=1", ct);

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
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["scope"] = "https://api.ebay.com/oauth/api_scope/sell.inventory https://api.ebay.com/oauth/api_scope/sell.fulfillment"
                });

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);

                var response = await _httpClient.PostAsync(TokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<EbayTokenResponse>(content, _json);
                if (token is null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken ?? integration.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products (Inventory API) ──────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var limit = filter?.PageSize ?? 100; // eBay max = 100
                var offset = ((filter?.Page ?? 1) - 1) * limit;
                var allProducts = new List<ExternalProduct>();
                var hasMore = true;

                while (hasMore)
                {
                    var response = await _httpClient.GetAsync(
                        $"{ApiBase}/sell/inventory/v1/inventory_item?limit={limit}&offset={offset}", ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var ebayResponse = JsonSerializer.Deserialize<EbayInventoryResponse>(content, _json);
                    if (ebayResponse?.InventoryItems is not null)
                        allProducts.AddRange(ebayResponse.InventoryItems.Select(MapToExternalProduct));

                    var total = ebayResponse?.Total ?? 0;
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
                    $"{ApiBase}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(externalId)}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var item = JsonSerializer.Deserialize<EbayInventoryItem>(content, _json);
                if (item is null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(item));
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

                var sku = product.Sku ?? Guid.NewGuid().ToString("N")[..12];
                var body = new
                {
                    product = new
                    {
                        title = product.Name,
                        description = product.Description ?? string.Empty,
                        aspects = new Dictionary<string, string[]>(),
                        imageUrls = product.ImageUrl is not null
                            ? new[] { product.ImageUrl } : Array.Empty<string>()
                    },
                    condition = "NEW",
                    availability = new
                    {
                        shipToLocationAvailability = new
                        {
                            quantity = product.StockQuantity
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{ApiBase}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(sku)}", request, ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create inventory item: {content}",
                        statusCode: (int)response.StatusCode);

                // eBay: بعد إنشاء inventory item، لازم تعمل offer وتنشره
                var offerResult = await CreateAndPublishOfferAsync(integration, sku, product, ct);
                if (!offerResult.IsSuccess)
                    return AdapterResult<string>.Failure(
                        $"Item created but offer failed: {offerResult.ErrorMessage}");

                return AdapterResult<string>.Success(sku);
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

                var sku = product.Sku ?? product.ExternalId;
                var body = new
                {
                    product = new
                    {
                        title = product.Name,
                        description = product.Description ?? string.Empty
                    },
                    condition = "NEW",
                    availability = new
                    {
                        shipToLocationAvailability = new { quantity = product.StockQuantity }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{ApiBase}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(sku)}", request, ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to update product: {content}",
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

                var response = await _httpClient.DeleteAsync(
                    $"{ApiBase}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(externalId)}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to delete product: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders (Fulfillment API) ──────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var limit = filter?.PageSize ?? 50;
                var allOrders = new List<ExternalOrder>();
                var offset = ((filter?.Page ?? 1) - 1) * limit;
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{ApiBase}/sell/fulfillment/v1/order?limit={limit}&offset={offset}&orderingstatus=ACTIVE";

                    if (filter?.ModifiedAfter != null)
                        url += $"&filter=lastModifiedDate:[{filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}..{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}]";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var ebayResponse = JsonSerializer.Deserialize<EbayOrdersResponse>(content, _json);
                    if (ebayResponse?.Orders is not null)
                        allOrders.AddRange(ebayResponse.Orders.Select(MapToExternalOrder));

                    var total = ebayResponse?.Total ?? 0;
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

                var response = await _httpClient.GetAsync(
                    $"{ApiBase}/sell/fulfillment/v1/order/{externalId}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<EbayOrder>(content, _json);
                if (order is null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(order));
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

                if (newStatus.ToLower() == "shipped")
                {
                    // eBay: تأكيد الشحن عبر issueShipment
                    var body = new
                    {
                        lineItems = Array.Empty<object>(),
                        shippingCarrierCode = "USPS",
                        trackingNumber = string.Empty
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{ApiBase}/sell/fulfillment/v1/order/{externalId}/issueShipment",
                        request, ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult.Failure(
                            $"Failed to issue shipment: {content}",
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

                // eBay Bulk Update Inventory — max 25 per request
                var errors = new List<string>();
                var batches = items.Chunk(25);

                foreach (var batch in batches)
                {
                    var inventoryItems = batch.Select(item => new
                    {
                        sku = item.Sku ?? item.ExternalProductId,
                        availability = new
                        {
                            shipToLocationAvailability = new { quantity = item.Quantity }
                        }
                    }).ToArray();

                    var body = new { requests = inventoryItems };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{ApiBase}/sell/inventory/v1/bulk_update_price_quantity",
                        request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add(content);
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

        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // eBay Commerce Notification API
                var body = new
                {
                    topicId = "marketplace.account.deletion",
                    deliveryConfig = new
                    {
                        endpoint = integration.WebhookSecret ?? string.Empty,
                        verificationToken = integration.WebhookSecret ?? string.Empty
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{ApiBase}/commerce/notification/v1/subscription", request, ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to register webhook: {content}",
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

                // جلب subscriptions أولاً
                var listResponse = await _httpClient.GetAsync(
                    $"{ApiBase}/commerce/notification/v1/subscription", ct);

                if (!listResponse.IsSuccessStatusCode)
                    return AdapterResult.Success(); // مفيش subscriptions

                var listContent = await listResponse.Content.ReadAsStringAsync(ct);
                var subs = JsonSerializer.Deserialize<EbaySubscriptionsResponse>(listContent, _json);

                if (subs?.Subscriptions is null || subs.Subscriptions.Count == 0)
                    return AdapterResult.Success();

                var errors = new List<string>();
                foreach (var sub in subs.Subscriptions)
                {
                    if (string.IsNullOrEmpty(sub.SubscriptionId)) continue;

                    var deleteResponse = await _httpClient.DeleteAsync(
                        $"{ApiBase}/commerce/notification/v1/subscription/{sub.SubscriptionId}", ct);

                    if (!deleteResponse.IsSuccessStatusCode)
                        errors.Add(sub.SubscriptionId);
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Failed to delete subscriptions: {string.Join(", ", errors)}")
                    : AdapterResult.Success();
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
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToBase64String(hash);

            return expected == signature;
        }

        // ── Private Helpers ───────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);
            _httpClient.DefaultRequestHeaders.Remove("X-EBAY-C-MARKETPLACE-ID");
            _httpClient.DefaultRequestHeaders.Add("X-EBAY-C-MARKETPLACE-ID", MarketplaceId);
        }

        private async Task<AdapterResult> CreateAndPublishOfferAsync(
            StoreIntegration integration,
            string sku,
            ExternalProduct product,
            CancellationToken ct)
        {
            try
            {
                var offerBody = new
                {
                    sku = sku,
                    marketplaceId = MarketplaceId,
                    format = "FIXED_PRICE",
                    listingDescription = product.Description ?? product.Name,
                    pricingSummary = new
                    {
                        price = new { value = product.Price.ToString("F2"), currency = "USD" }
                    },
                    merchantLocationKey = "DEFAULT",
                    categoryId = "9355" // General category — يُحدَّث حسب المنتج
                };

                var json = JsonSerializer.Serialize(offerBody, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var offerResponse = await _httpClient.PostAsync(
                    $"{ApiBase}/sell/inventory/v1/offer", request, ct);

                var offerContent = await offerResponse.Content.ReadAsStringAsync(ct);

                if (!offerResponse.IsSuccessStatusCode)
                    return AdapterResult.Failure($"Failed to create offer: {offerContent}");

                var offerResult = JsonSerializer.Deserialize<EbayOfferResponse>(offerContent, _json);
                var offerId = offerResult?.OfferId;

                if (string.IsNullOrEmpty(offerId))
                    return AdapterResult.Failure("Offer created but ID not returned");

                // نشر الـ offer
                var publishResponse = await _httpClient.PostAsync(
                    $"{ApiBase}/sell/inventory/v1/offer/{offerId}/publish",
                    new StringContent("{}", Encoding.UTF8, "application/json"), ct);

                return publishResponse.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure($"Failed to publish offer: {await publishResponse.Content.ReadAsStringAsync(ct)}");
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Mapping ───────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(EbayInventoryItem item) => new()
        {
            ExternalId = item.Sku ?? string.Empty,
            Name = item.Product?.Title ?? string.Empty,
            Description = item.Product?.Description,
            Sku = item.Sku,
            Price = 0, // السعر يجيء من الـ offer
            StockQuantity = item.Availability?.ShipToLocationAvailability?.Quantity ?? 0,
            IsActive = true,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault(),
            Categories = [],
            Variants = []
        };

        private static ExternalOrder MapToExternalOrder(EbayOrder o) => new()
        {
            ExternalId = o.OrderId ?? string.Empty,
            OrderNumber = o.LegacyOrderId ?? o.OrderId ?? string.Empty,
            Status = MapFromEbayOrderStatus(o.OrderFulfillmentStatus ?? string.Empty),
            TotalAmount = decimal.TryParse(o.PricingSummary?.Total?.Value, out var total) ? total : 0,
            Currency = o.PricingSummary?.Total?.Currency ?? "USD",
            Customer = o.Buyer is null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Buyer.Username ?? string.Empty,
                Name = o.Buyer.Username ?? string.Empty,
                Email = o.Buyer.TaxAddress?.Email ?? string.Empty
            },
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.LegacyItemId ?? string.Empty,
                ProductName = i.Title ?? string.Empty,
                Sku = i.Sku ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = decimal.TryParse(i.LineItemCost?.Value, out var p) ? p : 0,
                TotalPrice = decimal.TryParse(i.LineItemCost?.Value, out var tp)
                                        ? tp * i.Quantity : 0
            }).ToList() ?? [],
            ShippingAddress = o.FulfillmentStartInstructions?.FirstOrDefault()?.ShippingStep?.ShipTo is null
                ? null
                : new ExternalAddress
                {
                    Street = o.FulfillmentStartInstructions.First().ShippingStep!.ShipTo!.ContactAddress?.AddressLine1,
                    City = o.FulfillmentStartInstructions.First().ShippingStep!.ShipTo!.ContactAddress?.City,
                    Country = o.FulfillmentStartInstructions.First().ShippingStep!.ShipTo!.ContactAddress?.CountryCode,
                    PostalCode = o.FulfillmentStartInstructions.First().ShippingStep!.ShipTo!.ContactAddress?.PostalCode
                },
            CreatedAt = o.CreationDate,
            UpdatedAt = o.LastModifiedDate
        };

        private static string MapFromEbayOrderStatus(string ebayStatus) =>
            ebayStatus switch
            {
                "NOT_STARTED" => "pending",
                "IN_PROGRESS" => "processing",
                "FULFILLED" => "shipped",
                "FULLY_SHIPPED" => "shipped",
                _ => ebayStatus.ToLower()
            };
    }

    // ── eBay API Models ───────────────────────────────────────────────────────

    internal class EbayTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? TokenType { get; set; }
    }

    // — Inventory —
    internal class EbayInventoryResponse
    {
        public List<EbayInventoryItem>? InventoryItems { get; set; }
        public int Total { get; set; }
        public int Size { get; set; }
    }

    internal class EbayInventoryItem
    {
        public string? Sku { get; set; }
        public string? Condition { get; set; }
        public EbayProduct? Product { get; set; }
        public EbayAvailability? Availability { get; set; }
    }

    internal class EbayProduct
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? ImageUrls { get; set; }
        public List<string>? Aspects { get; set; }
    }

    internal class EbayAvailability
    {
        public EbayShipToAvailability? ShipToLocationAvailability { get; set; }
    }

    internal class EbayShipToAvailability
    {
        public int Quantity { get; set; }
    }

    internal class EbayOfferResponse
    {
        public string? OfferId { get; set; }
    }

    // — Orders —
    internal class EbayOrdersResponse
    {
        public List<EbayOrder>? Orders { get; set; }
        public int Total { get; set; }
    }

    internal class EbayOrder
    {
        public string? OrderId { get; set; }
        public string? LegacyOrderId { get; set; }
        public string? OrderFulfillmentStatus { get; set; }
        public EbayPricingSummary? PricingSummary { get; set; }
        public EbayBuyer? Buyer { get; set; }
        public List<EbayLineItem>? LineItems { get; set; }
        public List<EbayFulfillmentInstruction>? FulfillmentStartInstructions { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }
    }

    internal class EbayPricingSummary
    {
        public EbayAmount? Total { get; set; }
    }

    internal class EbayAmount
    {
        public string? Value { get; set; }
        public string? Currency { get; set; }
    }

    internal class EbayBuyer
    {
        public string? Username { get; set; }
        public EbayTaxAddress? TaxAddress { get; set; }
    }

    internal class EbayTaxAddress
    {
        public string? Email { get; set; }
    }

    internal class EbayLineItem
    {
        public string? LineItemId { get; set; }
        public string? LegacyItemId { get; set; }
        public string? Title { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public EbayAmount? LineItemCost { get; set; }
    }

    internal class EbayFulfillmentInstruction
    {
        public EbayShippingStep? ShippingStep { get; set; }
    }

    internal class EbayShippingStep
    {
        public EbayShipTo? ShipTo { get; set; }
    }

    internal class EbayShipTo
    {
        public EbayContactAddress? ContactAddress { get; set; }
    }

    internal class EbayContactAddress
    {
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? CountryCode { get; set; }
        public string? PostalCode { get; set; }
    }

    // — Webhooks —
    internal class EbaySubscriptionsResponse
    {
        public List<EbaySubscription>? Subscriptions { get; set; }
    }

    internal class EbaySubscription
    {
        public string? SubscriptionId { get; set; }
        public string? TopicId { get; set; }
        public string? Status { get; set; }
    }
}