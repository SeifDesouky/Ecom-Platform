using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Noon
{
    /// <summary>
    /// Noon Seller Commercial API
    ///
    /// متطلبات:
    ///   - يكون التاجر seller معتمد على noon (Seller Lab)
    ///   - يطلب API credentials من noon مباشرة عبر:
    ///     support.noon.partners → Authentication Credentials - Commercial APIs
    ///
    /// Auth:
    ///   - Basic Auth: Base64(apiKey:apiSecret) في كل request
    ///   - الـ ApiKey   بيتخزن في StoreIntegration.ApiKey
    ///   - الـ ApiSecret بيتخزن في StoreIntegration.ApiSecret
    ///   - الـ Country  (ae/sa/eg) بيتخزن في StoreIntegration.ExternalStoreId
    ///
    /// Base URLs:
    ///   - UAE: https://api.noon.partners/seller/v2/ae
    ///   - KSA: https://api.noon.partners/seller/v2/sa
    ///   - Egypt: https://api.noon.partners/seller/v2/eg
    ///
    /// Docs: https://support.noon.partners (Commercial API — requires approval)
    /// </summary>
    public class NoonAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Noon;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,  // noon مش بيشارك بيانات العميل الكاملة
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,
            SupportsOAuth = false,
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
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public NoonAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
        }

        // ── Base URL per country ──────────────────────────────────────────────

        /// <summary>
        /// ExternalStoreId بيحتوي على كود الدولة: ae | sa | eg
        /// Default: ae (UAE)
        /// </summary>
        private static string BaseUrl(StoreIntegration i)
        {
            var country = i.ExternalStoreId?.ToLower() ?? "ae";
            return $"https://api.noon.partners/seller/v2/{country}";
        }

        // ── Auth ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Noon بيستخدم Basic Auth: Base64(ApiKey:ApiSecret)
        /// </summary>
        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Clear();

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    $"{integration.ApiKey}:{integration.ApiSecret}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/orders?limit=1", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure(
                        "Invalid API credentials. Request credentials from noon Seller Lab.",
                        "UNAUTHORIZED", 401);

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
        /// Noon Basic Auth مش بتنتهي — مفيش refresh
        /// </summary>
        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default) =>
            Task.FromResult(
                AdapterResult<TokenData>.Failure(
                    "Noon uses Basic Auth — no token refresh needed.",
                    "NOT_SUPPORTED"));

        // ── Products ──────────────────────────────────────────────────────────

        /// <summary>
        /// GET {BaseUrl}/products?page={page}&limit={limit}
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var page = filter?.Page ?? 1;
                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl(integration)}/products?page={page}&limit={limit}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_at_min={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<NoonProductsResponse>(content, _json);
                var products = root?.Products?.Select(MapToExternalProduct).ToList()
                               ?? new List<ExternalProduct>();

                return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(products);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// GET {BaseUrl}/products/{sku}
        /// noon بيستخدم SKU كـ identifier مش numeric ID
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
                    $"{BaseUrl(integration)}/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<NoonProduct>(content, _json);
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
        /// POST {BaseUrl}/products
        /// noon بيتطلب catalog content موجود مسبقاً في Seller Lab
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
                    sku = product.Sku ?? product.ExternalId,
                    name = product.Name,
                    price = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "active" : "inactive"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl(integration)}/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<NoonProduct>(content, _json);
                var id = created?.Sku ?? created?.NoonId?.ToString();

                if (string.IsNullOrEmpty(id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(id);
            }
            catch (Exception ex)
            {
                return AdapterResult<string>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// PATCH {BaseUrl}/products/{sku}
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
                    price = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "active" : "inactive"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{BaseUrl(integration)}/products/{product.ExternalId}",
                    request, ct);

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
        /// noon مش بيسمح بحذف المنتجات عبر API — بس deactivate
        /// </summary>
        public async Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // noon مش بيدعم DELETE — بنعمل deactivate بدلاً منه
                var body = new { status = "inactive" };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{BaseUrl(integration)}/products/{externalId}", request, ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to deactivate product: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders ────────────────────────────────────────────────────────────

        /// <summary>
        /// GET {BaseUrl}/orders?page={page}&limit={limit}&status={status}
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var page = filter?.Page ?? 1;
                var limit = filter?.PageSize ?? 50;
                var url = $"{BaseUrl(integration)}/orders?page={page}&limit={limit}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_at_min={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<NoonOrdersResponse>(content, _json);
                var orders = root?.Orders?.Select(MapToExternalOrder).ToList()
                             ?? new List<ExternalOrder>();

                return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(orders);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// GET {BaseUrl}/orders/{orderId}
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
                    $"{BaseUrl(integration)}/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<NoonOrder>(content, _json);
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
        /// POST {BaseUrl}/orders/{orderId}/shipments  — لتأكيد الشحن
        /// POST {BaseUrl}/orders/{orderId}/cancel     — للإلغاء
        /// noon بيستخدم actions مش status update مباشرة
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

                var endpoint = MapToNoonOrderAction(newStatus);
                var body = new { status = newStatus };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl(integration)}/orders/{externalId}/{endpoint}",
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

        // ── Inventory ─────────────────────────────────────────────────────────

        /// <summary>
        /// GET {BaseUrl}/inventory
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/inventory", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {content}");

                var root = JsonSerializer.Deserialize<NoonInventoryResponse>(content, _json);
                var inventory = root?.Items?.Select(i => new ExternalInventory
                {
                    ExternalProductId = i.Sku ?? string.Empty,
                    Sku = i.Sku,
                    Quantity = i.Quantity ?? 0,
                    LocationId = i.WarehouseCode
                }).ToList() ?? new List<ExternalInventory>();

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(inventory);
            }
            catch (Exception ex)
            {
                return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// PATCH {BaseUrl}/inventory
        /// Body: [{ sku, quantity, warehouse_code }]
        /// </summary>
        public async Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var body = items.Select(i => new
                {
                    sku = i.Sku ?? i.ExternalProductId,
                    quantity = i.Quantity,
                    warehouse_code = i.LocationId ?? "default"
                }).ToList();

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(
                    $"{BaseUrl(integration)}/inventory", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update inventory: {content}",
                        statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks ──────────────────────────────────────────────────────────

        /// <summary>
        /// noon بيدعم webhooks عبر FBPI (Fulfilled by Partner Integration)
        /// بيتسجل من Seller Lab مش عبر API مباشرة —
        /// الـ webhook URL بيتحدد في إعدادات الـ warehouse في Seller Lab
        /// </summary>
        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default) =>
            Task.FromResult(
                AdapterResult.Failure(
                    "Noon webhooks are configured via Seller Lab (FBPI Warehouse settings), not via API. " +
                    "Set webhook URL to: https://rahtk.sa/api/webhooks/noon",
                    "MANUAL_SETUP_REQUIRED"));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default) =>
            Task.FromResult(
                AdapterResult.Failure(
                    "Noon webhooks must be unregistered manually via Seller Lab.",
                    "MANUAL_SETUP_REQUIRED"));

        /// <summary>
        /// noon بيبعت signature في header X-Noon-Signature
        /// Algorithm: HMAC-SHA256(payload, WebhookSecret)
        /// </summary>
        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
        {
            if (string.IsNullOrEmpty(integration.WebhookSecret)) return false;

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = Convert.ToHexString(hash).ToLower();

            return computed == signature?.ToLower();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string MapToNoonOrderAction(string status) =>
            status.ToLower() switch
            {
                "shipped" => "shipments",
                "delivered" => "shipments",
                "cancelled" => "cancel",
                _ => "confirm"
            };

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(NoonProduct p) => new()
        {
            ExternalId = p.Sku ?? p.NoonId?.ToString() ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price ?? 0,
            StockQuantity = p.Quantity ?? 0,
            IsActive = p.Status == "active",
            ImageUrl = p.ImageUrl,
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(NoonOrder o) => new()
        {
            ExternalId = o.OrderId ?? string.Empty,
            OrderNumber = o.OrderNr ?? o.OrderId ?? string.Empty,
            Status = o.Status ?? "pending",
            TotalAmount = o.Total ?? 0,
            Currency = o.Currency ?? "AED",

            // noon مش بيشارك بيانات العميل الكاملة لأسباب privacy
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId,
                Name = o.CustomerName,
                Email = null,
                Phone = null
            },

            Items = (o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.Sku ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.UnitPrice * i.Quantity
            }) ?? []).ToList(),

            ShippingAddress = new ExternalAddress
            {
                Street = o.ShippingAddress,
                City = o.ShippingCity,
                Country = o.ShippingCountry,
                PostalCode = o.ShippingPostcode,
                Phone = null
            },

            CreatedAt = o.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = o.UpdatedAt
        };
    }

    // ── Noon API Models ───────────────────────────────────────────────────────

    internal class NoonProductsResponse
    {
        public List<NoonProduct>? Products { get; set; }
    }

    internal class NoonProduct
    {
        public int? NoonId { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Status { get; set; }   // active | inactive
        public string? ImageUrl { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class NoonOrdersResponse
    {
        public List<NoonOrder>? Orders { get; set; }
    }

    internal class NoonOrder
    {
        public string? OrderId { get; set; }
        public string? OrderNr { get; set; }
        public string? Status { get; set; }
        public decimal? Total { get; set; }
        public string? Currency { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public List<NoonItem>? Items { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingCountry { get; set; }
        public string? ShippingPostcode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class NoonItem
    {
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    internal class NoonInventoryResponse
    {
        public List<NoonInventoryItem>? Items { get; set; }
    }

    internal class NoonInventoryItem
    {
        public string? Sku { get; set; }
        public int? Quantity { get; set; }
        public string? WarehouseCode { get; set; }
    }
}