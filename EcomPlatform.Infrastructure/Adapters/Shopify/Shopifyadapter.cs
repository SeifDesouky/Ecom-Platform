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

namespace EcomPlatform.Infrastructure.Adapters.Shopify
{
    /// <summary>
    /// Shopify Admin REST API 2024-01
    /// Docs: https://shopify.dev/docs/api/admin-rest
    /// Auth: OAuth2 أو Private App (API Key + Password)
    /// </summary>
    public class ShopifyAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Shopify;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,
            SupportsOAuth = true,
            SupportsApiKey = true,
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

        public ShopifyAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Shopify:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Shopify:ClientSecret"] ?? string.Empty;
        }

        // ── Base URL per store ────────────────────────────────────────────────

        /// <summary>
        /// Shopify الـ base URL بيختلف لكل متجر:
        /// https://{store}.myshopify.com/admin/api/2024-01
        /// الـ StoreUrl بيتخزن في StoreIntegration.StoreUrl
        /// </summary>
        private static string BaseUrl(StoreIntegration i) =>
            $"{i.StoreUrl?.TrimEnd('/')}/admin/api/2024-01";

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/shop.json", ct);

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
            // Shopify access tokens مش بتنتهي — مفيش refresh flow
            // لو OAuth بيستخدم offline token دايم صالح
            // لو في مشكلة المستخدم يعيد الـ OAuth flow من الداشبورد
            return await Task.FromResult(
                AdapterResult<TokenData>.Failure(
                    "Shopify tokens do not expire. Re-authenticate via OAuth if needed.",
                    "NOT_SUPPORTED"));
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

                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl(integration)}/products.json?limit={limit}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_at_min={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                // Shopify بيستخدم page_info للـ pagination
                if (!string.IsNullOrEmpty(filter?.Cursor))
                    url += $"&page_info={filter.Cursor}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ShopifyProductsResponse>(content, _json);
                var products = root?.Products?.Select(MapToExternalProduct).ToList()
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

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/products/{externalId}.json", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ShopifyProductResponse>(content, _json);
                if (root?.Product == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(root.Product));
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

                var body = new
                {
                    product = new
                    {
                        title = product.Name,
                        body_html = product.Description ?? string.Empty,
                        vendor = "EcomPlatform",
                        status = product.IsActive ? "active" : "draft",
                        variants = new[]
                        {
                            new
                            {
                                price     = product.Price.ToString("F2"),
                                sku       = product.Sku ?? string.Empty,
                                inventory_quantity = product.StockQuantity
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl(integration)}/products.json", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ShopifyProductResponse>(content, _json);
                var id = root?.Product?.Id.ToString();

                if (string.IsNullOrEmpty(id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(id);
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
                    product = new
                    {
                        id = product.ExternalId,
                        title = product.Name,
                        body_html = product.Description ?? string.Empty,
                        status = product.IsActive ? "active" : "draft",
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl(integration)}/products/{product.ExternalId}.json", request, ct);

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
                    $"{BaseUrl(integration)}/products/{externalId}.json", ct);

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
                SetAuthHeaders(integration);

                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl(integration)}/orders.json?limit={limit}&status=any";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_at_min={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                if (!string.IsNullOrEmpty(filter?.Cursor))
                    url += $"&page_info={filter.Cursor}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ShopifyOrdersResponse>(content, _json);
                var orders = root?.Orders?.Select(MapToExternalOrder).ToList()
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
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/orders/{externalId}.json", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ShopifyOrderResponse>(content, _json);
                if (root?.Order == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(root.Order));
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

                // Shopify بيستخدم fulfillments لتغيير status
                // "shipped" → create fulfillment | "cancelled" → cancel order
                if (newStatus.ToLower() == "cancelled")
                {
                    var cancelBody = new StringContent("{}", Encoding.UTF8, "application/json");
                    var cancelResponse = await _httpClient.PostAsync(
                        $"{BaseUrl(integration)}/orders/{externalId}/cancel.json",
                        cancelBody, ct);

                    return cancelResponse.IsSuccessStatusCode
                        ? AdapterResult.Success()
                        : AdapterResult.Failure(
                            $"Failed to cancel order: {cancelResponse.StatusCode}",
                            statusCode: (int)cancelResponse.StatusCode);
                }

                // باقي الحالات عبر fulfillment
                var body = new
                {
                    fulfillment = new
                    {
                        status = MapToShopifyFulfillmentStatus(newStatus)
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl(integration)}/orders/{externalId}/fulfillments.json",
                    request, ct);

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
            try
            {
                SetAuthHeaders(integration);

                // Shopify Inventory API — بيتطلب location_id
                var locResponse = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/locations.json", ct);
                var locContent = await locResponse.Content.ReadAsStringAsync(ct);

                if (!locResponse.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        "Failed to get locations");

                var locations = JsonSerializer.Deserialize<ShopifyLocationsResponse>(locContent, _json);
                var locationId = locations?.Locations?.FirstOrDefault()?.Id;

                if (locationId == null)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        "No locations found");

                var invResponse = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/inventory_levels.json?location_ids={locationId}", ct);
                var invContent = await invResponse.Content.ReadAsStringAsync(ct);

                if (!invResponse.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {invContent}");

                var invData = JsonSerializer.Deserialize<ShopifyInventoryResponse>(invContent, _json);
                var inventory = invData?.InventoryLevels?.Select(i => new ExternalInventory
                {
                    ExternalProductId = i.InventoryItemId.ToString(),
                    Quantity = i.Available ?? 0
                }).ToList() ?? new List<ExternalInventory>();

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(inventory);
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

                // جيب أول location
                var locResponse = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/locations.json", ct);
                var locContent = await locResponse.Content.ReadAsStringAsync(ct);
                var locations = JsonSerializer.Deserialize<ShopifyLocationsResponse>(locContent, _json);
                var locationId = locations?.Locations?.FirstOrDefault()?.Id;

                if (locationId == null)
                    return AdapterResult.Failure("No locations found");

                var errors = new List<string>();

                foreach (var item in items)
                {
                    var body = new
                    {
                        location_id = locationId,
                        inventory_item_id = item.ExternalProductId,
                        available = item.Quantity
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl(integration)}/inventory_levels/set.json", request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Item {item.ExternalProductId}: {content}");
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
                        webhook = new
                        {
                            topic = MapToShopifyWebhookTopic(eventType),
                            address = $"https://rahtk.sa/api/webhooks/shopify",
                            format = "json"
                        }
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl(integration)}/webhooks.json", request, ct);

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

        public async Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/webhooks.json", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure("Failed to list webhooks");

                var webhooks = JsonSerializer.Deserialize<ShopifyWebhooksResponse>(content, _json);
                if (webhooks?.Webhooks == null) return AdapterResult.Success();

                foreach (var wh in webhooks.Webhooks)
                {
                    await _httpClient.DeleteAsync(
                        $"{BaseUrl(integration)}/webhooks/{wh.Id}.json", ct);
                }

                return AdapterResult.Success();
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
            if (string.IsNullOrEmpty(_clientSecret)) return false;

            // Shopify بيستخدم HMAC-SHA256 على الـ raw body مع الـ client secret
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_clientSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = Convert.ToBase64String(hash);

            return computed == signature;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            // Shopify Private App: X-Shopify-Access-Token
            _httpClient.DefaultRequestHeaders.Add(
                "X-Shopify-Access-Token", integration.ApiKey ?? string.Empty);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private static string MapToShopifyFulfillmentStatus(string status) =>
            status.ToLower() switch
            {
                "shipped" => "success",
                "delivered" => "success",
                _ => "pending"
            };

        private static string MapToShopifyWebhookTopic(string eventType) =>
            eventType switch
            {
                "product.created" => "products/create",
                "product.updated" => "products/update",
                "product.deleted" => "products/delete",
                "order.created" => "orders/create",
                "order.updated" => "orders/updated",
                "order.canceled" => "orders/cancelled",
                "inventory.updated" => "inventory_levels/update",
                _ => eventType
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(ShopifyProduct p) => new()
        {
            ExternalId = p.Id.ToString(),
            Name = p.Title ?? string.Empty,
            Description = p.BodyHtml,
            Sku = p.Variants?.FirstOrDefault()?.Sku,
            Price = p.Variants?.FirstOrDefault()?.Price ?? 0,
            StockQuantity = p.Variants?.Sum(v => v.InventoryQuantity) ?? 0,
            IsActive = p.Status == "active",
            ImageUrl = p.Images?.FirstOrDefault()?.Src,
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(ShopifyOrder o) => new()
        {
            ExternalId = o.Id.ToString(),
            OrderNumber = o.Name ?? o.OrderNumber.ToString(),
            Status = o.FinancialStatus ?? o.FulfillmentStatus ?? "pending",
            TotalAmount = o.TotalPrice,
            Currency = o.Currency ?? "USD",
            Customer = o.Customer == null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Customer.Id.ToString(),
                Name = $"{o.Customer.FirstName} {o.Customer.LastName}".Trim(),
                Email = o.Customer.Email,
                Phone = o.Customer.Phone
            },
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId.ToString(),
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                TotalPrice = i.Price * i.Quantity
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Address1,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.Country,
                PostalCode = o.ShippingAddress.Zip,
                Phone = o.ShippingAddress.Phone
            },
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };
    }

    // ── Shopify API Models ────────────────────────────────────────────────────

    internal class ShopifyProductsResponse
    {
        public List<ShopifyProduct>? Products { get; set; }
    }

    internal class ShopifyProductResponse
    {
        public ShopifyProduct? Product { get; set; }
    }

    internal class ShopifyProduct
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? BodyHtml { get; set; }
        public string? Status { get; set; }
        public List<ShopifyVariant>? Variants { get; set; }
        public List<ShopifyImage>? Images { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class ShopifyVariant
    {
        public long Id { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int InventoryQuantity { get; set; }
        public long? InventoryItemId { get; set; }
    }

    internal class ShopifyImage
    {
        public string? Src { get; set; }
    }

    internal class ShopifyOrdersResponse
    {
        public List<ShopifyOrder>? Orders { get; set; }
    }

    internal class ShopifyOrderResponse
    {
        public ShopifyOrder? Order { get; set; }
    }

    internal class ShopifyOrder
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int OrderNumber { get; set; }
        public string? FinancialStatus { get; set; }
        public string? FulfillmentStatus { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Currency { get; set; }
        public ShopifyCustomer? Customer { get; set; }
        public List<ShopifyLineItem>? LineItems { get; set; }
        public ShopifyAddress? ShippingAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class ShopifyCustomer
    {
        public long Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    internal class ShopifyLineItem
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    internal class ShopifyAddress
    {
        public string? Address1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
    }

    internal class ShopifyLocationsResponse
    {
        public List<ShopifyLocation>? Locations { get; set; }
    }

    internal class ShopifyLocation
    {
        public long Id { get; set; }
        public string? Name { get; set; }
    }

    internal class ShopifyInventoryResponse
    {
        public List<ShopifyInventoryLevel>? InventoryLevels { get; set; }
    }

    internal class ShopifyInventoryLevel
    {
        public long InventoryItemId { get; set; }
        public long LocationId { get; set; }
        public int? Available { get; set; }
    }

    internal class ShopifyWebhooksResponse
    {
        public List<ShopifyWebhook>? Webhooks { get; set; }
    }

    internal class ShopifyWebhook
    {
        public long Id { get; set; }
        public string? Topic { get; set; }
        public string? Address { get; set; }
    }
}