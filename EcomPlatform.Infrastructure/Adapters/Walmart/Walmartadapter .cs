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

namespace EcomPlatform.Infrastructure.Adapters.Walmart
{
    /// <summary>
    /// Walmart Marketplace Adapter
    /// Auth: OAuth2 Client Credentials + Signature Authentication (WM_SEC.*)
    /// Docs: https://developer.walmart.com/api/us/mp
    /// </summary>
    public class WalmartAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://marketplace.walmartapis.com/v3";
        private const string TokenUrl = "https://marketplace.walmartapis.com/v3/token";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.WalmartMarketplace;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = false,  // Walmart يستخدم polling
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = true,
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
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public WalmartAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Walmart:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Walmart:ClientSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/items?limit=1", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid Walmart credentials", "UNAUTHORIZED", 401);

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
                // Walmart: Client Credentials (لا يوجد refresh token — نطلب access token جديد)
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

                var requestMsg = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
                requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                requestMsg.Headers.Add("WM_SVC.NAME", "Walmart Marketplace");
                requestMsg.Headers.Add("WM_QOS.CORRELATION_ID", Guid.NewGuid().ToString());
                requestMsg.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                });

                var response = await _httpClient.SendAsync(requestMsg, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token request failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<WalmartTokenResponse>(content, _json);
                if (token is null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = null, // Client Credentials لا يوجد refresh token
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products (Items API) ──────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var limit = filter?.PageSize ?? 100; // Walmart max = 100
                var allProducts = new List<ExternalProduct>();
                var nextCursor = string.Empty;
                var isFirst = true;

                do
                {
                    var url = isFirst
                        ? $"{BaseUrl}/items?limit={limit}"
                        : $"{BaseUrl}/items?limit={limit}&nextCursor={Uri.EscapeDataString(nextCursor)}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var walmartResponse = JsonSerializer.Deserialize<WalmartItemsResponse>(content, _json);
                    if (walmartResponse?.ItemsResponse is not null)
                        allProducts.AddRange(walmartResponse.ItemsResponse.Select(MapToExternalProduct));

                    nextCursor = walmartResponse?.NextCursor ?? string.Empty;
                    isFirst = false;

                    if (filter != null && filter.Page > 0) break;

                } while (!string.IsNullOrEmpty(nextCursor));

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
                    $"{BaseUrl}/items/{externalId}?idType=SKU", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var item = JsonSerializer.Deserialize<WalmartItem>(content, _json);
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
                    MPItemFeedHeader = new { version = "4.2", requestId = Guid.NewGuid().ToString() },
                    MPItem = new[]
                    {
                        new
                        {
                            processMode      = "REPLACE",
                            sku              = sku,
                            productIdentifiers = new { productIdType = "GTIN", productId = sku },
                            MPOffer          = new
                            {
                                price             = product.Price,
                                currencyUnit      = "USD",
                                fulfillmentLagTime = 1
                            },
                            MPProduct = new
                            {
                                productName  = product.Name,
                                shortDescription = product.Description ?? string.Empty,
                                mainImageUrl     = product.ImageUrl ?? string.Empty,
                                category         = "General"
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/feeds?feedType=MP_ITEM", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create item: {content}",
                        statusCode: (int)response.StatusCode);

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
            // Walmart: التحديث يتم عبر نفس الـ feed بـ processMode = REPLACE
            var createResult = await CreateProductAsync(integration, product, ct);
            return createResult.IsSuccess
                ? AdapterResult.Success()
                : AdapterResult.Failure(createResult.ErrorMessage ?? "Update failed");
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
                    $"{BaseUrl}/items/{externalId}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to retire item: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders ───────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var limit = filter?.PageSize ?? 200; // Walmart max = 200
                var allOrders = new List<ExternalOrder>();
                var nextCursor = string.Empty;
                var isFirst = true;

                var createdStartDate = (filter?.ModifiedAfter ?? DateTime.UtcNow.AddDays(-7))
                    .ToString("yyyy-MM-ddTHH:mm:ssZ");

                do
                {
                    var url = isFirst
                        ? $"{BaseUrl}/orders?limit={limit}&createdStartDate={Uri.EscapeDataString(createdStartDate)}"
                        : $"{BaseUrl}/orders?limit={limit}&nextCursor={Uri.EscapeDataString(nextCursor)}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var walmartResponse = JsonSerializer.Deserialize<WalmartOrdersResponse>(content, _json);

                    if (walmartResponse?.List?.Elements?.Order is not null)
                        allOrders.AddRange(walmartResponse.List.Elements.Order.Select(MapToExternalOrder));

                    nextCursor = walmartResponse?.List?.Meta?.NextCursor ?? string.Empty;
                    isFirst = false;

                    if (filter != null && filter.Page > 0) break;

                } while (!string.IsNullOrEmpty(nextCursor));

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
                    $"{BaseUrl}/orders/{externalId}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<WalmartOrder>(content, _json);
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
                    var body = new
                    {
                        orderShipment = new
                        {
                            orderLines = new
                            {
                                orderLine = Array.Empty<object>()
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/orders/{externalId}/shipping", request, ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult.Failure(
                            $"Failed to ship order: {content}",
                            statusCode: (int)response.StatusCode);
                }
                else if (newStatus.ToLower() == "cancelled")
                {
                    var body = new { orderCancellation = new { orderLines = new { orderLine = Array.Empty<object>() } } };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/orders/{externalId}/cancel", request, ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult.Failure(
                            $"Failed to cancel order: {content}",
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
            try
            {
                SetAuthHeaders(integration);

                var allInventory = new List<ExternalInventory>();
                var nextCursor = string.Empty;
                var isFirst = true;

                do
                {
                    var url = isFirst
                        ? $"{BaseUrl}/inventories?limit=50"
                        : $"{BaseUrl}/inventories?limit=50&nextCursor={Uri.EscapeDataString(nextCursor)}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                            $"Failed to get inventory: {content}",
                            statusCode: (int)response.StatusCode);

                    var invResponse = JsonSerializer.Deserialize<WalmartInventoryResponse>(content, _json);

                    if (invResponse?.Elements?.InventoryList is not null)
                    {
                        allInventory.AddRange(invResponse.Elements.InventoryList.Select(i => new ExternalInventory
                        {
                            ExternalProductId = i.Sku ?? string.Empty,
                            Sku = i.Sku,
                            Quantity = i.Quantity?.Amount ?? 0
                        }));
                    }

                    nextCursor = invResponse?.NextCursor ?? string.Empty;
                    isFirst = false;

                } while (!string.IsNullOrEmpty(nextCursor));

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(allInventory);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure($"Error: {ex.Message}");
            }
        }

        public async Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // Walmart: Bulk inventory update عبر feeds
                var inventoryList = items.Select(item => new
                {
                    sku = item.Sku ?? item.ExternalProductId,
                    quantity = new { unit = "EACH", amount = item.Quantity }
                }).ToArray();

                var body = new
                {
                    InventoryHeader = new { version = "1.4" },
                    Inventory = inventoryList
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/feeds?feedType=inventory", request, ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to update inventory: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks (Walmart يستخدم polling — لا يدعم webhooks رسمياً) ──

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Failure(
                "Walmart Marketplace does not support webhooks. Use polling via BackgroundSyncJob.",
                "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Success());

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature) => false;

        // ── Private Helpers ───────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", integration.ApiKey);

            _httpClient.DefaultRequestHeaders.Remove("WM_SVC.NAME");
            _httpClient.DefaultRequestHeaders.Remove("WM_QOS.CORRELATION_ID");
            _httpClient.DefaultRequestHeaders.Remove("WM_SEC.ACCESS_TOKEN");
            _httpClient.DefaultRequestHeaders.Remove("WM_CONSUMER.CHANNEL.TYPE");

            _httpClient.DefaultRequestHeaders.Add("WM_SVC.NAME", "Walmart Marketplace");
            _httpClient.DefaultRequestHeaders.Add("WM_QOS.CORRELATION_ID", Guid.NewGuid().ToString());
            _httpClient.DefaultRequestHeaders.Add("WM_SEC.ACCESS_TOKEN", integration.ApiKey ?? string.Empty);
            _httpClient.DefaultRequestHeaders.Add("WM_CONSUMER.CHANNEL.TYPE", "SWAGGER_CHANNEL_TYPE");
        }

        // ── Mapping ───────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(WalmartItem item) => new()
        {
            ExternalId = item.ItemId ?? item.Sku ?? string.Empty,
            Name = item.ProductName ?? string.Empty,
            Description = item.ShortDescription,
            Sku = item.Sku,
            Price = item.Price ?? 0,
            StockQuantity = item.InventoryCount ?? 0,
            IsActive = item.PublishStatus == "PUBLISHED",
            ImageUrl = item.MainImageUrl,
            Categories = item.Category is not null ? [item.Category] : [],
            Variants = []
        };

        private static ExternalOrder MapToExternalOrder(WalmartOrder o) => new()
        {
            ExternalId = o.PurchaseOrderId ?? string.Empty,
            OrderNumber = o.CustomerOrderId ?? o.PurchaseOrderId ?? string.Empty,
            Status = MapFromWalmartOrderStatus(o.OrderLines?.OrderLine?.FirstOrDefault()?.OrderLineStatuses?.OrderLineStatus?.FirstOrDefault()?.Status ?? string.Empty),
            TotalAmount = o.OrderLines?.OrderLine?.Sum(l => l.Charges?.Charge?.Sum(c => c.ChargeAmount?.Amount ?? 0) ?? 0) ?? 0,
            Currency = "USD",
            Customer = o.ShippingInfo is null ? null : new ExternalCustomerInfo
            {
                Name = o.ShippingInfo.PostalAddress?.Name ?? string.Empty,
                Email = string.Empty
            },
            Items = o.OrderLines?.OrderLine?.Select(l => new ExternalOrderItem
            {
                ExternalProductId = l.Item?.ProductId ?? string.Empty,
                ProductName = l.Item?.ProductName ?? string.Empty,
                Sku = l.Item?.Sku ?? string.Empty,
                Quantity = (int)(l.OrderedQuantity?.Amount ?? 0),
                UnitPrice = l.Charges?.Charge?.FirstOrDefault()?.ChargeAmount?.Amount ?? 0,
                TotalPrice = l.Charges?.Charge?.Sum(c => c.ChargeAmount?.Amount ?? 0) ?? 0
            }).ToList() ?? [],
            ShippingAddress = o.ShippingInfo?.PostalAddress is null ? null : new ExternalAddress
            {
                Street = o.ShippingInfo.PostalAddress.Address1,
                City = o.ShippingInfo.PostalAddress.City,
                Country = o.ShippingInfo.PostalAddress.Country,
                PostalCode = o.ShippingInfo.PostalAddress.PostalCode
            },
            CreatedAt = o.OrderDate != 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(o.OrderDate).UtcDateTime
                : DateTime.UtcNow,
            UpdatedAt = null
        };

        private static string MapFromWalmartOrderStatus(string walmartStatus) =>
            walmartStatus.ToUpper() switch
            {
                "CREATED" => "pending",
                "ACKNOWLEDGED" => "confirmed",
                "SHIPPED" => "shipped",
                "DELIVERED" => "delivered",
                "CANCELLED" => "cancelled",
                "REFUND" => "returned",
                _ => walmartStatus.ToLower()
            };
    }

    // ── Walmart API Models ────────────────────────────────────────────────────

    internal class WalmartTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }

    // — Items —
    internal class WalmartItemsResponse
    {
        public List<WalmartItem>? ItemsResponse { get; set; }
        public string? NextCursor { get; set; }
        public int TotalItems { get; set; }
    }

    internal class WalmartItem
    {
        public string? ItemId { get; set; }
        public string? Sku { get; set; }
        public string? ProductName { get; set; }
        public string? ShortDescription { get; set; }
        public string? MainImageUrl { get; set; }
        public decimal? Price { get; set; }
        public int? InventoryCount { get; set; }
        public string? PublishStatus { get; set; }
        public string? Category { get; set; }
    }

    // — Orders —
    internal class WalmartOrdersResponse
    {
        public WalmartOrderList? List { get; set; }
    }

    internal class WalmartOrderList
    {
        public WalmartOrderMeta? Meta { get; set; }
        public WalmartOrderElements? Elements { get; set; }
    }

    internal class WalmartOrderMeta
    {
        public string? NextCursor { get; set; }
        public int TotalCount { get; set; }
    }

    internal class WalmartOrderElements
    {
        public List<WalmartOrder>? Order { get; set; }
    }

    internal class WalmartOrder
    {
        public string? PurchaseOrderId { get; set; }
        public string? CustomerOrderId { get; set; }
        public long OrderDate { get; set; }
        public WalmartShippingInfo? ShippingInfo { get; set; }
        public WalmartOrderLines? OrderLines { get; set; }
    }

    internal class WalmartShippingInfo
    {
        public WalmartPostalAddress? PostalAddress { get; set; }
    }

    internal class WalmartPostalAddress
    {
        public string? Name { get; set; }
        public string? Address1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }

    internal class WalmartOrderLines
    {
        public List<WalmartOrderLine>? OrderLine { get; set; }
    }

    internal class WalmartOrderLine
    {
        public WalmartOrderLineItem? Item { get; set; }
        public WalmartOrderedQuantity? OrderedQuantity { get; set; }
        public WalmartCharges? Charges { get; set; }
        public WalmartOrderLineStatuses? OrderLineStatuses { get; set; }
    }

    internal class WalmartOrderLineItem
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
    }

    internal class WalmartOrderedQuantity
    {
        public decimal Amount { get; set; }
        public string? Unit { get; set; }
    }

    internal class WalmartCharges
    {
        public List<WalmartCharge>? Charge { get; set; }
    }

    internal class WalmartCharge
    {
        public string? ChargeType { get; set; }
        public WalmartAmount? ChargeAmount { get; set; }
    }

    internal class WalmartAmount
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
    }

    internal class WalmartOrderLineStatuses
    {
        public List<WalmartOrderLineStatus>? OrderLineStatus { get; set; }
    }

    internal class WalmartOrderLineStatus
    {
        public string? Status { get; set; }
    }

    // — Inventory —
    internal class WalmartInventoryResponse
    {
        public WalmartInventoryElements? Elements { get; set; }
        public string? NextCursor { get; set; }
    }

    internal class WalmartInventoryElements
    {
        public List<WalmartInventoryItem>? InventoryList { get; set; }
    }

    internal class WalmartInventoryItem
    {
        public string? Sku { get; set; }
        public WalmartQuantity? Quantity { get; set; }
    }

    internal class WalmartQuantity
    {
        public string? Unit { get; set; }
        public int Amount { get; set; }
    }
}