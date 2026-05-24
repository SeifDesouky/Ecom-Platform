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

namespace EcomPlatform.Infrastructure.Adapters.InstagramShop
{
    /// <summary>
    /// Meta Commerce API (Instagram Shop + Facebook Shop — نفس الـ API)
    /// Docs: https://developers.facebook.com/docs/commerce-platform
    /// Auth: OAuth2 — Page Access Token
    /// ملاحظة: Instagram Shop بيشتغل عبر Facebook Catalog — نفس الـ Catalog بيظهر على الاتنين
    /// </summary>
    public class InstagramShopAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://graph.facebook.com/v19.0";

        private readonly HttpClient _httpClient;
        private readonly string _appId;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.InstagramShop;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,   // عبر Meta Commerce — محدود
            SupportsCustomers = false,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,
            SupportsOAuth = true,
            SupportsApiKey = false,
            SupportsBulkSync = true,   // Batch API
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
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public InstagramShopAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appId = configuration["InstagramShop:AppId"] ?? string.Empty;
            _appSecret = configuration["InstagramShop:AppSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var token = integration.ApiKey;
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/me?access_token={token}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Connection failed: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<MetaUserResponse>(content, _json);
                return !string.IsNullOrEmpty(result?.Id)
                    ? AdapterResult.Success()
                    : AdapterResult.Failure("Invalid token response");
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
                // Meta بيدعم long-lived tokens — بنجدد الـ token قبل ما ينتهي (60 يوم)
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/oauth/access_token" +
                    $"?grant_type=fb_exchange_token" +
                    $"&client_id={_appId}" +
                    $"&client_secret={_appSecret}" +
                    $"&fb_exchange_token={integration.ApiKey}", ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<MetaTokenResponse>(content, _json);
                if (token == null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.AccessToken, // Meta مش عنده refresh token منفصل
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products (Catalog Items) ──────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                var catalogId = integration.ExternalStoreId;
                if (string.IsNullOrEmpty(catalogId))
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        "Catalog ID (ExternalStoreId) is required");

                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl}/{catalogId}/products" +
                            $"?access_token={integration.ApiKey}" +
                            $"&limit={limit}" +
                            $"&fields=id,name,description,price,currency,availability,inventory,image_url,retailer_id";

                if (!string.IsNullOrEmpty(filter?.Cursor))
                    url += $"&after={filter.Cursor}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<MetaListResponse<MetaProduct>>(content, _json);
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
                var url = $"{BaseUrl}/{externalId}" +
                               $"?access_token={integration.ApiKey}" +
                               $"&fields=id,name,description,price,currency,availability,inventory,image_url,retailer_id";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<MetaProduct>(content, _json);
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
                var catalogId = integration.ExternalStoreId;
                var url = $"{BaseUrl}/{catalogId}/products?access_token={integration.ApiKey}";

                var body = new
                {
                    retailer_id = product.Sku ?? Guid.NewGuid().ToString(),
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    price = (int)(product.Price * 100), // Meta بيستخدم cents
                    currency = "SAR",
                    availability = product.StockQuantity > 0 ? "in stock" : "out of stock",
                    inventory = product.StockQuantity,
                    image_url = product.ImageUrl ?? string.Empty,
                    url = product.ImageUrl ?? string.Empty, // product page URL
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

                var result = JsonSerializer.Deserialize<MetaCreateResponse>(content, _json);
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
                var url = $"{BaseUrl}/{product.ExternalId}?access_token={integration.ApiKey}";

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
                var response = await _httpClient.PostAsync(url, request, ct); // Meta بيستخدم POST للـ update

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
                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/{externalId}?access_token={integration.ApiKey}", ct);

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

        // ── Orders ───────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                // Meta Commerce Orders — محتاج commerce_account_id
                var accountId = integration.ExternalStoreId;
                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl}/{accountId}/commerce_orders" +
                                $"?access_token={integration.ApiKey}" +
                                $"&limit={limit}" +
                                $"&fields=id,order_status,created,last_updated,items,shipping_address,buyer_details,selected_shipping_option";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<MetaListResponse<MetaOrder>>(content, _json);
                var orders = root?.Data?.Select(MapToExternalOrder).ToList()
                    ?? new List<ExternalOrder>();

                return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(orders);
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
                var url = $"{BaseUrl}/{externalId}" +
                               $"?access_token={integration.ApiKey}" +
                               $"&fields=id,order_status,created,last_updated,items,shipping_address,buyer_details";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<MetaOrder>(content, _json);
                if (order == null)
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
                var metaStatus = MapToMetaOrderStatus(newStatus);
                if (metaStatus == null)
                    return AdapterResult.Failure($"Unsupported status: {newStatus}", "NOT_SUPPORTED");

                var url = $"{BaseUrl}/{externalId}?access_token={integration.ApiKey}";
                var body = new { order_status = new { state = metaStatus } };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update order status: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
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
                // Meta Batch API — أسرع من loop
                var catalogId = integration.ExternalStoreId;
                var requests = items.Select(item => new
                {
                    method = "POST",
                    relative_url = $"{item.ExternalProductId}",
                    body = $"inventory={item.Quantity}&availability={(item.Quantity > 0 ? "in+stock" : "out+of+stock")}"
                }).ToList();

                var batchBody = new
                {
                    access_token = integration.ApiKey,
                    batch = requests
                };

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
                // Meta Webhooks — بتتسجل على الـ App مش على كل integration
                var url = $"{BaseUrl}/{_appId}/subscriptions?access_token={integration.ApiKey}";
                var body = new
                {
                    object_ = "commerce_account",
                    callback_url = "https://rahtk.sa/api/webhooks/instagram",
                    verify_token = integration.WebhookSecret ?? string.Empty,
                    fields = string.Join(",", eventTypes.Select(MapToMetaWebhookField))
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);

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
                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/{_appId}/subscriptions?access_token={integration.ApiKey}", ct);

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

            // Meta: X-Hub-Signature-256 = sha256=HMAC(app_secret, payload)
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string? MapToMetaOrderStatus(string status) =>
            status.ToLower() switch
            {
                "shipped" => "SHIPPED",
                "delivered" => "DELIVERED",
                "cancelled" => "CANCELLED",
                "refunded" => "REFUNDED",
                _ => null
            };

        private static string MapToMetaWebhookField(string eventType) =>
            eventType switch
            {
                "order.created" or "order.updated" => "orders",
                "product.created" or "product.updated" => "product_catalog",
                _ => eventType
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(MetaProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.RetailerId,
            Price = p.Price / 100m, // Meta بيخزن بـ cents
            StockQuantity = p.Inventory ?? 0,
            IsActive = p.Availability == "in stock",
            ImageUrl = p.ImageUrl,
        };

        private static ExternalOrder MapToExternalOrder(MetaOrder o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.Id ?? string.Empty,
            Status = o.OrderStatus?.State ?? "CREATED",
            TotalAmount = o.Items?.Sum(i => i.PricePerUnit * i.Quantity) ?? 0,
            Currency = "SAR",
            Customer = o.BuyerDetails == null ? null : new ExternalCustomerInfo
            {
                Name = o.BuyerDetails.Name,
                Email = o.BuyerDetails.Email,
            },
            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Street1,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.Country,
                PostalCode = o.ShippingAddress.PostalCode,
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.RetailerId ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.PricePerUnit,
                TotalPrice = i.PricePerUnit * i.Quantity
            }).ToList() ?? [],
            CreatedAt = o.Created ?? DateTime.UtcNow,
        };
    }

    // ── Meta API Models ───────────────────────────────────────────────────────

    internal class MetaUserResponse
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    internal class MetaTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class MetaListResponse<T>
    {
        public List<T>? Data { get; set; }
        public MetaPaging? Paging { get; set; }
    }

    internal class MetaPaging
    {
        public MetaCursors? Cursors { get; set; }
        public string? Next { get; set; }
    }

    internal class MetaCursors
    {
        public string? Before { get; set; }
        public string? After { get; set; }
    }

    internal class MetaCreateResponse
    {
        public string? Id { get; set; }
    }

    internal class MetaProduct
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? RetailerId { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? Availability { get; set; }
        public int? Inventory { get; set; }
        public string? ImageUrl { get; set; }
    }

    internal class MetaOrder
    {
        public string? Id { get; set; }
        public MetaOrderStatus? OrderStatus { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<MetaOrderItem>? Items { get; set; }
        public MetaShippingAddress? ShippingAddress { get; set; }
        public MetaBuyerDetails? BuyerDetails { get; set; }
    }

    internal class MetaOrderStatus
    {
        public string? State { get; set; }
    }

    internal class MetaOrderItem
    {
        public string? Id { get; set; }
        public string? RetailerId { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal PricePerUnit { get; set; }
    }

    internal class MetaShippingAddress
    {
        public string? Street1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }

    internal class MetaBuyerDetails
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}