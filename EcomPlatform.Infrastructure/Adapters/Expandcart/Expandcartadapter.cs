using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.ExpandCart
{
    /// <summary>
    /// ExpandCart Admin REST API
    /// المنصة مبنية على OpenCart engine — نفس نظام الـ API
    ///
    /// Auth Flow (مهم):
    ///   1. POST {StoreUrl}/index.php?route=api/login
    ///      Body: username={ApiKey}&key={ApiSecret}   (x-www-form-urlencoded)
    ///      Response: { "api_token": "xxxxx" }
    ///   2. كل request تاني بيبعت: ?api_token={api_token}
    ///
    /// الـ StoreUrl بيتخزن في StoreIntegration.StoreUrl
    /// الـ API Username بيتخزن في StoreIntegration.ApiKey
    /// الـ API Key    بيتخزن في StoreIntegration.ApiSecret
    ///
    /// ملاحظة: ExpandCart مش عنده Public API رسمي — الـ endpoints دي
    /// مبنية على OpenCart 3.x default API + ExpandCart extensions
    /// لو المتجر عنده custom endpoint يتعدل الـ route بناءً عليه
    /// </summary>
    public class ExpandCartAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.ExpandCart;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = false,   // OpenCart مش بيدعم webhooks natively
            SupportsOAuth = false,
            SupportsApiKey = true,
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
                SyncEntityType.Customers,
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public ExpandCartAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
        }

        // ── Base URL ──────────────────────────────────────────────────────────

        private static string Base(StoreIntegration i) =>
            i.StoreUrl?.TrimEnd('/') ?? string.Empty;

        // ── Auth: Login → api_token ───────────────────────────────────────────

        /// <summary>
        /// بيعمل login ويرجع api_token للـ session الحالية
        /// POST {StoreUrl}/index.php?route=api/login
        /// Body: username=ApiKey&key=ApiSecret
        /// </summary>
        private async Task<string?> GetSessionTokenAsync(
            StoreIntegration integration,
            CancellationToken ct)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = integration.ApiKey ?? string.Empty,
                ["key"] = integration.ApiSecret ?? string.Empty
            });

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync(
                $"{Base(integration)}/index.php?route=api/login", body, ct);

            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ExpandCartLoginResponse>(content, _json);
            return result?.ApiToken;
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);

                if (string.IsNullOrEmpty(token))
                    return AdapterResult.Failure(
                        "Login failed — check API Username and API Key",
                        "UNAUTHORIZED", 401);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// OpenCart/ExpandCart مش بيستخدم OAuth —
        /// الـ session token بيتجدد تلقائياً مع كل login
        /// </summary>
        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default) =>
            Task.FromResult(
                AdapterResult<TokenData>.Failure(
                    "ExpandCart uses session-based auth. No token refresh needed — re-login automatically.",
                    "NOT_SUPPORTED"));

        // ── Products ──────────────────────────────────────────────────────────

        /// <summary>
        /// GET {StoreUrl}/index.php?route=api/product/products&api_token=xxx
        /// Params: limit, page, filter_date_modified
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                var limit = filter?.PageSize ?? 50;
                var page = filter?.Page ?? 1;
                var url = $"{Base(integration)}/index.php?route=api/product/products" +
                            $"&api_token={token}&limit={limit}&page={page}";

                if (filter?.ModifiedAfter != null)
                    url += $"&filter_date_modified={filter.ModifiedAfter:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ExpandCartProductsResponse>(content, _json);
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
        /// GET {StoreUrl}/index.php?route=api/product/product&api_token=xxx&product_id={id}
        /// </summary>
        public async Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                var url = $"{Base(integration)}/index.php?route=api/product/product" +
                               $"&api_token={token}&product_id={externalId}";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<ExpandCartProduct>(content, _json);
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
        /// POST {StoreUrl}/index.php?route=api/product/product&api_token=xxx
        /// </summary>
        public async Task<AdapterResult<string>> CreateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<string>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                // OpenCart بيستخدم product_description nested object للاسم والوصف
                var body = new
                {
                    product_description = new Dictionary<string, object>
                    {
                        ["1"] = new   // language_id=1 (English/Arabic default)
                        {
                            name = product.Name,
                            description = product.Description ?? string.Empty,
                            meta_title = product.Name,
                            meta_description = string.Empty,
                            meta_keyword = string.Empty,
                            tag = string.Empty
                        }
                    },
                    model = product.Sku ?? product.Name,
                    sku = product.Sku ?? string.Empty,
                    price = product.Price.ToString("F2"),
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "1" : "0",
                    product_store = new[] { "0" },     // default store
                    product_category = Array.Empty<string>()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{Base(integration)}/index.php?route=api/product/product&api_token={token}",
                    request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<ExpandCartCreateResponse>(content, _json);
                var id = result?.ProductId?.ToString();

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
        /// PUT {StoreUrl}/index.php?route=api/product/product&api_token=xxx&product_id={id}
        /// </summary>
        public async Task<AdapterResult> UpdateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult.Failure("Authentication failed", "UNAUTHORIZED", 401);

                var body = new
                {
                    product_description = new Dictionary<string, object>
                    {
                        ["1"] = new
                        {
                            name = product.Name,
                            description = product.Description ?? string.Empty,
                            meta_title = product.Name,
                            meta_description = string.Empty,
                            meta_keyword = string.Empty,
                            tag = string.Empty
                        }
                    },
                    price = product.Price.ToString("F2"),
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "1" : "0"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{Base(integration)}/index.php?route=api/product/product" +
                    $"&api_token={token}&product_id={product.ExternalId}",
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
        /// DELETE {StoreUrl}/index.php?route=api/product/product&api_token=xxx&product_id={id}
        /// </summary>
        public async Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult.Failure("Authentication failed", "UNAUTHORIZED", 401);

                var response = await _httpClient.DeleteAsync(
                    $"{Base(integration)}/index.php?route=api/product/product" +
                    $"&api_token={token}&product_id={externalId}", ct);

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

        // ── Orders ────────────────────────────────────────────────────────────

        /// <summary>
        /// GET {StoreUrl}/index.php?route=api/order/orders&api_token=xxx
        /// Params: limit, page, filter_date_modified
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                var limit = filter?.PageSize ?? 50;
                var page = filter?.Page ?? 1;
                var url = $"{Base(integration)}/index.php?route=api/order/orders" +
                            $"&api_token={token}&limit={limit}&page={page}";

                if (filter?.ModifiedAfter != null)
                    url += $"&filter_date_modified={filter.ModifiedAfter:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var root = JsonSerializer.Deserialize<ExpandCartOrdersResponse>(content, _json);
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
        /// GET {StoreUrl}/index.php?route=api/order/order&api_token=xxx&order_id={id}
        /// </summary>
        public async Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                var url = $"{Base(integration)}/index.php?route=api/order/order" +
                               $"&api_token={token}&order_id={externalId}";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<ExpandCartOrder>(content, _json);
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
        /// PUT {StoreUrl}/index.php?route=api/order/history&api_token=xxx&order_id={id}
        /// Body: { order_status_id: int, notify: 1, comment: "" }
        /// OpenCart بيستخدم order_status_id (رقم) مش string
        /// </summary>
        public async Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult.Failure("Authentication failed", "UNAUTHORIZED", 401);

                var body = new
                {
                    order_status_id = MapToExpandCartStatusId(newStatus),
                    notify = 1,
                    comment = string.Empty
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{Base(integration)}/index.php?route=api/order/history" +
                    $"&api_token={token}&order_id={externalId}",
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
        /// ExpandCart/OpenCart مش عنده inventory endpoint مستقل —
        /// بنجيب الـ products وبناخد الـ quantity منهم
        /// </summary>
        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        "Authentication failed", "UNAUTHORIZED", 401);

                var url = $"{Base(integration)}/index.php?route=api/product/products" +
                               $"&api_token={token}&limit=250";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {content}");

                var root = JsonSerializer.Deserialize<ExpandCartProductsResponse>(content, _json);
                var inventory = root?.Products?.Select(p => new ExternalInventory
                {
                    ExternalProductId = p.ProductId?.ToString() ?? string.Empty,
                    Sku = p.Sku,
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
        /// OpenCart بيعمل update للـ inventory عبر product update
        /// PUT {StoreUrl}/index.php?route=api/product/product&api_token=xxx&product_id={id}
        /// Body: { quantity: int }
        /// </summary>
        public async Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
        {
            try
            {
                var token = await GetSessionTokenAsync(integration, ct);
                if (token == null)
                    return AdapterResult.Failure("Authentication failed", "UNAUTHORIZED", 401);

                var errors = new List<string>();

                foreach (var item in items)
                {
                    var body = new { quantity = item.Quantity };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PutAsync(
                        $"{Base(integration)}/index.php?route=api/product/product" +
                        $"&api_token={token}&product_id={item.ExternalProductId}",
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

        // ── Webhooks ──────────────────────────────────────────────────────────

        /// <summary>
        /// OpenCart/ExpandCart مش بيدعم webhooks natively —
        /// الـ sync بيتم عبر polling (BackgroundSyncJob)
        /// </summary>
        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default) =>
            Task.FromResult(
                AdapterResult.Failure(
                    "ExpandCart does not support webhooks natively. Use polling sync instead.",
                    "NOT_SUPPORTED"));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Success());

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature) => false; // غير مدعوم

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// OpenCart default order status IDs:
        /// 1=Pending, 2=Processing, 3=Shipped, 5=Complete, 7=Cancelled, 11=Refunded
        /// قد تختلف في ExpandCart — يتأكد من System > Localisation > Order Statuses
        /// </summary>
        private static int MapToExpandCartStatusId(string status) =>
            status.ToLower() switch
            {
                "pending" => 1,
                "processing" => 2,
                "shipped" => 3,
                "delivered" => 5,
                "cancelled" => 7,
                "refunded" => 11,
                _ => 1
            };

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(ExpandCartProduct p) => new()
        {
            ExternalId = p.ProductId?.ToString() ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price ?? 0,
            StockQuantity = p.Quantity ?? 0,
            IsActive = p.Status == "1",
            ImageUrl = p.Image,
            UpdatedAt = p.DateModified
        };

        private static ExternalOrder MapToExternalOrder(ExpandCartOrder o) => new()
        {
            ExternalId = o.OrderId?.ToString() ?? string.Empty,
            OrderNumber = o.OrderId?.ToString() ?? string.Empty,
            Status = o.OrderStatus ?? "pending",
            TotalAmount = o.Total ?? 0,
            Currency = o.CurrencyCode ?? "SAR",

            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId?.ToString(),
                Name = $"{o.Firstname} {o.Lastname}".Trim(),
                Email = o.Email,
                Phone = o.Telephone
            },

            Items = (o.Products?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId?.ToString() ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                TotalPrice = i.Total
            }) ?? []).ToList(),

            ShippingAddress = new ExternalAddress
            {
                Street = o.ShippingAddress1,
                City = o.ShippingCity,
                Country = o.ShippingCountry,
                PostalCode = o.ShippingPostcode,
                Phone = o.Telephone
            },

            CreatedAt = o.DateAdded ?? DateTime.UtcNow,
            UpdatedAt = o.DateModified
        };
    }

    // ── ExpandCart / OpenCart API Models ──────────────────────────────────────

    internal class ExpandCartLoginResponse
    {
        public string? ApiToken { get; set; }
    }

    internal class ExpandCartProductsResponse
    {
        public List<ExpandCartProduct>? Products { get; set; }
    }

    internal class ExpandCartProduct
    {
        public int? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public string? Model { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? Status { get; set; }   // "1" = enabled | "0" = disabled
        public string? Image { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class ExpandCartCreateResponse
    {
        public int? ProductId { get; set; }
    }

    internal class ExpandCartOrdersResponse
    {
        public List<ExpandCartOrder>? Orders { get; set; }
    }

    internal class ExpandCartOrder
    {
        public int? OrderId { get; set; }
        public int? CustomerId { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? OrderStatus { get; set; }
        public decimal? Total { get; set; }
        public string? CurrencyCode { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingCountry { get; set; }
        public string? ShippingPostcode { get; set; }
        public List<ExpandCartOrderItem>? Products { get; set; }
        public DateTime? DateAdded { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class ExpandCartOrderItem
    {
        public int? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }
}