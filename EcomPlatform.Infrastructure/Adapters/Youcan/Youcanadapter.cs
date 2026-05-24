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

namespace EcomPlatform.Infrastructure.Adapters.YouCan
{
    /// <summary>
    /// YouCan Store Admin REST API
    /// Docs:  https://developer.youcan.shop/store-admin/introduction/getting-started
    /// Auth:  OAuth2 — Bearer token في كل request
    /// Base:  https://api.youcan.shop
    /// Webhook Signature: HMAC-SHA256 على الـ payload بالـ OAuth Client Secret
    ///        Header: x-youcan-signature
    /// </summary>
    public class YouCanAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private const string BaseUrl = "https://api.youcan.shop";

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.YouCan;

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
                SyncEntityType.Inventory
            ]
        };

        public YouCanAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["YouCan:ClientId"] ?? string.Empty;
            _clientSecret = configuration["YouCan:ClientSecret"] ?? string.Empty;
        }

        // ── Auth Headers ─────────────────────────────────────────────────────

        /// <summary>
        /// YouCan بيستخدم Bearer token — الـ Access Token بيتخزن في StoreIntegration.ApiKey
        /// </summary>
        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey ?? string.Empty);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/store", ct);

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

        /// <summary>
        /// YouCan OAuth2 — access_token بينتهي بعد 1,295,999 ثانية (~15 يوم)
        /// Refresh باستخدام grant_type=refresh_token
        /// Docs: https://developer.youcan.shop/store-admin/introduction/oauth
        /// </summary>
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
                    ["client_secret"] = _clientSecret,
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty
                });

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/oauth/token", body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<YouCanTokenResponse>(content, _json);
                if (token?.AccessToken == null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    TokenType = token.TokenType ?? "Bearer",
                    // YouCan بيبعت expires_in بالثواني (~1,295,999 ثانية = ~15 يوم)
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products ─────────────────────────────────────────────────────────

        /// <summary>
        /// GET https://api.youcan.shop/products
        /// Params: limit, page, updated_after (ISO8601)
        /// Docs: https://developer.youcan.shop/store-admin/products/listing
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = $"{BaseUrl}/products?limit={filter?.PageSize ?? 50}&page={filter?.Page ?? 1}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_after={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<YouCanProductsResponse>(content, _json);
                var products = root?.Data?.Select(MapToExternalProduct).ToList()
                               ?? new List<ExternalProduct>();

                return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(products);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// GET https://api.youcan.shop/products/{id}
        /// Docs: https://developer.youcan.shop/store-admin/products/get
        /// </summary>
        public async Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<YouCanProduct>(content, _json);
                if (product == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(product));
            }
            catch (Exception ex)
            {
                return AdapterResult<ExternalProduct>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// POST https://api.youcan.shop/products
        /// Docs: https://developer.youcan.shop/store-admin/products/create
        /// </summary>
        public async Task<AdapterResult<string>> CreateProductAsync(
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
                    price = product.Price,
                    quantity = product.StockQuantity,
                    sku = product.Sku ?? string.Empty,
                    status = product.IsActive ? "published" : "draft"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<YouCanProduct>(content, _json);
                if (string.IsNullOrEmpty(created?.Id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(created.Id);
            }
            catch (Exception ex)
            {
                return AdapterResult<string>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// PUT https://api.youcan.shop/products/{id}
        /// Docs: https://developer.youcan.shop/store-admin/products/update
        /// </summary>
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
                    price = product.Price,
                    status = product.IsActive ? "published" : "draft"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl}/products/{product.ExternalId}", request, ct);

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

        /// <summary>
        /// DELETE https://api.youcan.shop/products/{id}
        /// Docs: https://developer.youcan.shop/store-admin/products/delete
        /// </summary>
        public async Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/products/{externalId}", ct);

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

        /// <summary>
        /// GET https://api.youcan.shop/orders
        /// Params: limit, page
        /// Docs: https://developer.youcan.shop/store-admin/orders/listing
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = $"{BaseUrl}/orders?limit={filter?.PageSize ?? 50}&page={filter?.Page ?? 1}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<YouCanOrdersResponse>(content, _json);
                var orders = root?.Data?.Select(MapToExternalOrder).ToList()
                             ?? new List<ExternalOrder>();

                return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(orders);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// GET https://api.youcan.shop/orders/{id}
        /// Docs: https://developer.youcan.shop/store-admin/orders/get
        /// </summary>
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

                var order = JsonSerializer.Deserialize<YouCanOrder>(content, _json);
                if (order == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(order));
            }
            catch (Exception ex)
            {
                return AdapterResult<ExternalOrder>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// POST https://api.youcan.shop/orders/{id}/update-status
        /// Docs: https://developer.youcan.shop/store-admin/orders/update_status
        /// Statuses: pending | processing | shipped | delivered | cancelled | refunded
        /// </summary>
        public async Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var body = new { status = MapToYouCanOrderStatus(newStatus) };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/orders/{externalId}/update-status", request, ct);

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

        /// <summary>
        /// YouCan مفيش endpoint مستقل للـ inventory listing —
        /// بنجيب الـ products وبناخد منهم الـ quantity
        /// Docs: https://developer.youcan.shop/store-admin/product-inventory/increment
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync($"{BaseUrl}/products?limit=250", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {content}");

                var root = JsonSerializer.Deserialize<YouCanProductsResponse>(content, _json);
                var inventory = root?.Data?.Select(p => new ExternalInventory
                {
                    ExternalProductId = p.Id ?? string.Empty,
                    Quantity = p.Quantity ?? 0
                }).ToList() ?? new List<ExternalInventory>();

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(inventory);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// YouCan بيستخدم increment/decrement مش set مباشر —
        /// بنحسب الفرق ونعمل increment أو decrement بناءً عليه
        /// POST https://api.youcan.shop/products/{id}/inventory/increment
        /// POST https://api.youcan.shop/products/{id}/inventory/decrement
        /// </summary>
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
                    // جيب الكمية الحالية أولاً
                    var productResponse = await _httpClient.GetAsync(
                        $"{BaseUrl}/products/{item.ExternalProductId}", ct);

                    if (!productResponse.IsSuccessStatusCode)
                    {
                        errors.Add($"Item {item.ExternalProductId}: product not found");
                        continue;
                    }

                    var productContent = await productResponse.Content.ReadAsStringAsync(ct);
                    var product = JsonSerializer.Deserialize<YouCanProduct>(productContent, _json);
                    var currentQty = product?.Quantity ?? 0;
                    var diff = item.Quantity - currentQty;

                    if (diff == 0) continue;

                    var endpoint = diff > 0 ? "increment" : "decrement";
                    var body = new { quantity = Math.Abs(diff) };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/products/{item.ExternalProductId}/inventory/{endpoint}",
                        request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var err = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Item {item.ExternalProductId}: {err}");
                    }
                }

                return errors.Count == 0
                    ? AdapterResult.Success()
                    : AdapterResult.Failure($"Some updates failed: {string.Join(" | ", errors)}");
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks ─────────────────────────────────────────────────────────

        /// <summary>
        /// YouCan REST Hooks — Subscribe
        /// POST https://api.youcan.shop/rest-hooks/subscribe
        /// Body: { event: string, target_url: string }
        /// Docs: https://developer.youcan.shop/store-admin/resthooks/subscribe
        /// </summary>
        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var errors = new List<string>();

                foreach (var eventType in eventTypes)
                {
                    var body = new
                    {
                        @event = MapToYouCanEvent(eventType),
                        target_url = "https://rahtk.sa/api/webhooks/youcan"
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/rest-hooks/subscribe", request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"{eventType}: {content}");
                    }
                }

                return errors.Count == 0
                    ? AdapterResult.Success()
                    : AdapterResult.Failure($"Some webhooks failed: {string.Join(" | ", errors)}");
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// DELETE https://api.youcan.shop/rest-hooks/unsubscribe/{id}
        /// بنجيب القائمة الكاملة أولاً ثم نحذف كل واحد
        /// Docs: https://developer.youcan.shop/store-admin/resthooks/unsubscribe
        /// </summary>
        public async Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync($"{BaseUrl}/rest-hooks", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure("Failed to list webhooks");

                var hooks = JsonSerializer.Deserialize<YouCanHooksResponse>(content, _json);
                if (hooks?.Data == null) return AdapterResult.Success();

                foreach (var hook in hooks.Data)
                {
                    await _httpClient.DeleteAsync(
                        $"{BaseUrl}/rest-hooks/unsubscribe/{hook.Id}", ct);
                }

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Webhook signature verification
        /// Header: x-youcan-signature
        /// Algorithm: HMAC-SHA256(payload, OAuthClientSecret)
        /// Docs: https://developer.youcan.shop/store-admin/resthooks/listing
        /// </summary>
        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
        {
            if (string.IsNullOrEmpty(_clientSecret)) return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_clientSecret));
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var hash = hmac.ComputeHash(payloadBytes);
            var computed = Convert.ToHexString(hash).ToLower();

            return computed == signature?.ToLower();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string MapToYouCanOrderStatus(string status) =>
            status.ToLower() switch
            {
                "pending" => "pending",
                "processing" => "processing",
                "shipped" => "shipped",
                "delivered" => "delivered",
                "cancelled" => "cancelled",
                "refunded" => "refunded",
                _ => "pending"
            };

        private static string MapToYouCanEvent(string eventType) =>
            eventType switch
            {
                "order.created" => "order:created",
                "order.updated" => "order:updated",
                "order.canceled" => "order:cancelled",
                "product.created" => "product:created",
                "product.updated" => "product:updated",
                "product.deleted" => "product:deleted",
                _ => eventType
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(YouCanProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price ?? 0,
            StockQuantity = p.Quantity ?? 0,
            IsActive = p.Status == "published",
            ImageUrl = p.Thumbnail,
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(YouCanOrder o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.Reference ?? o.Id ?? string.Empty,
            Status = o.Status ?? "pending",
            TotalAmount = o.Total ?? 0,
            Currency = o.Currency ?? "SAR",

            Customer = o.Customer == null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Customer.Id,
                Name = $"{o.Customer.FirstName} {o.Customer.LastName}".Trim(),
                Email = o.Customer.Email,
                Phone = o.Customer.Phone
            },

            Items = (o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                TotalPrice = i.Price * i.Quantity
            }) ?? []).ToList(),

            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Address,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.Country,
                PostalCode = o.ShippingAddress.PostalCode,
                Phone = o.ShippingAddress.Phone
            },

            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };
    }

    // ── YouCan API Models ─────────────────────────────────────────────────────

    internal class YouCanTokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? TokenType { get; set; }
    }

    internal class YouCanProductsResponse
    {
        public List<YouCanProduct>? Data { get; set; }
    }

    internal class YouCanProduct
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Status { get; set; }   // published | draft
        public string? Thumbnail { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class YouCanOrdersResponse
    {
        public List<YouCanOrder>? Data { get; set; }
    }

    internal class YouCanOrder
    {
        public string? Id { get; set; }
        public string? Reference { get; set; }
        public string? Status { get; set; }
        public decimal? Total { get; set; }
        public string? Currency { get; set; }
        public YouCanCustomer? Customer { get; set; }
        public List<YouCanItem>? Items { get; set; }
        public YouCanAddress? ShippingAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class YouCanCustomer
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    internal class YouCanItem
    {
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    internal class YouCanAddress
    {
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
    }

    internal class YouCanHooksResponse
    {
        public List<YouCanHook>? Data { get; set; }
    }

    internal class YouCanHook
    {
        public string? Id { get; set; }
        public string? Event { get; set; }
        public string? TargetUrl { get; set; }
    }
}