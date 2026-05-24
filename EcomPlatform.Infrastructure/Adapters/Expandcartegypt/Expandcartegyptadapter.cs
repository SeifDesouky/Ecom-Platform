using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.ExpandCartEgypt
{
    /// <summary>
    /// ExpandCart Egypt Adapter.
    /// Auth: API Key per-store (stored in StoreIntegration.ApiKey).
    /// StoreUrl: The merchant's ExpandCart Egypt store domain (e.g. https://mystore.expandcart.com).
    /// Webhooks: Supported via HMAC-SHA256 (secret stored in StoreIntegration.WebhookSecret).
    /// Note: ExpandCart Egypt shares the same API structure as ExpandCart (Gulf),
    ///       but targets the Egyptian market with EGP currency defaults.
    /// </summary>
    public class ExpandCartEgyptAdapter : IMarketplaceAdapter
    {
        // ExpandCart Egypt uses the same REST API path convention as ExpandCart Gulf.
        // The base URL is per-store: {StoreUrl}/index.php?route=api/
        private const string ApiPath = "index.php?route=api/";

        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.ExpandCartEgypt;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = true,
            SupportsOAuth = false,
            SupportsApiKey = true,
            SupportsBulkSync = false,
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

        public ExpandCartEgyptAdapter(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var url = BuildUrl(integration, "product&limit=1");
                SetAuthHeaders(integration);

                var response = await _httpClient.GetAsync(url, ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid API key", "UNAUTHORIZED", 401);

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
        /// ExpandCart Egypt uses API Key authentication — no OAuth token refresh needed.
        /// </summary>
        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult<TokenData>.Failure(
                "ExpandCart Egypt uses API Key authentication. Token refresh is not applicable.",
                "NOT_SUPPORTED",
                statusCode: 501));

        // ── Products ─────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var page = filter?.Page ?? 1;
                var pageSize = filter?.PageSize ?? 50;
                var start = (page - 1) * pageSize;

                var url = BuildUrl(integration, $"product&start={start}&limit={pageSize}");

                if (filter?.ModifiedAfter != null)
                    url += $"&date_modified={filter.ModifiedAfter:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var ecResponse = JsonSerializer.Deserialize<EcListResponse<EcProduct>>(content, _json);
                var products = ecResponse?.Products?.Select(MapToExternalProduct).ToList()
                                 ?? [];

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

                var url = BuildUrl(integration, $"product&product_id={externalId}");
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var ecResponse = JsonSerializer.Deserialize<EcSingleResponse<EcProduct>>(content, _json);
                if (ecResponse?.Product == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(ecResponse.Product));
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
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    model = product.Sku ?? string.Empty,
                    sku = product.Sku ?? string.Empty,
                    price = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "1" : "0",
                    // ExpandCart Egypt requires at minimum one category and tax_class_id
                    tax_class_id = "0",
                    category_id = product.Categories?.FirstOrDefault() ?? "1"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var url = BuildUrl(integration, "product");
                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var ecResponse = JsonSerializer.Deserialize<EcSingleResponse<EcProduct>>(content, _json);
                var createdId = ecResponse?.Product?.ProductId?.ToString();

                if (string.IsNullOrEmpty(createdId))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(createdId);
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
                    product_id = product.ExternalId,
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    model = product.Sku ?? string.Empty,
                    sku = product.Sku ?? string.Empty,
                    price = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "1" : "0"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var url = BuildUrl(integration, "product");
                var response = await _httpClient.PutAsync(url, request, ct);
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

                var url = BuildUrl(integration, $"product&product_id={externalId}");
                var response = await _httpClient.DeleteAsync(url, ct);
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

        // ── Orders ───────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var page = filter?.Page ?? 1;
                var pageSize = filter?.PageSize ?? 50;
                var start = (page - 1) * pageSize;

                var url = BuildUrl(integration, $"order&start={start}&limit={pageSize}");

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var ecResponse = JsonSerializer.Deserialize<EcListResponse<EcOrder>>(content, _json);
                var orders = ecResponse?.Orders?.Select(MapToExternalOrder).ToList()
                                 ?? [];

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

                var url = BuildUrl(integration, $"order&order_id={externalId}");
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var ecResponse = JsonSerializer.Deserialize<EcSingleResponse<EcOrder>>(content, _json);
                if (ecResponse?.Order == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(
                    MapToExternalOrder(ecResponse.Order));
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

                var body = new
                {
                    order_id = externalId,
                    order_status_id = MapToEcOrderStatus(newStatus),
                    notify = false,
                    comment = string.Empty
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var url = BuildUrl(integration, "order");
                var response = await _httpClient.PutAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to update order status: {content}",
                        statusCode: (int)response.StatusCode);

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
                    productsResult.ErrorMessage ?? "Failed to retrieve inventory");

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
                    var body = new
                    {
                        product_id = item.ExternalProductId,
                        quantity = item.Quantity
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var url = BuildUrl(integration, "product");
                    var response = await _httpClient.PutAsync(url, request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Product {item.ExternalProductId}: {content}");
                    }
                }

                if (errors.Count > 0)
                    return AdapterResult.Failure(
                        $"Some inventory updates failed: {string.Join(" | ", errors)}");

                return AdapterResult.Success();
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

                // ExpandCart Egypt webhook registration endpoint
                var body = new
                {
                    events = eventTypes,
                    callback = $"{integration.StoreUrl}/webhooks/expandcart-egypt"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var url = BuildUrl(integration, "webhook");
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
                SetAuthHeaders(integration);

                var url = BuildUrl(integration, "webhook");
                var response = await _httpClient.DeleteAsync(url, ct);

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
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string BuildUrl(StoreIntegration integration, string endpoint)
        {
            var baseUrl = integration.StoreUrl?.TrimEnd('/') ?? string.Empty;
            return $"{baseUrl}/{ApiPath}{endpoint}";
        }

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Oc-Merchant-Id");
            _httpClient.DefaultRequestHeaders.Remove("X-Oc-Merchant-Language");

            // ExpandCart REST API uses the API key as a Bearer token
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);

            // Some ExpandCart Egypt stores require the store ID header
            if (!string.IsNullOrEmpty(integration.ExternalStoreId))
                _httpClient.DefaultRequestHeaders.Add(
                    "X-Oc-Merchant-Id", integration.ExternalStoreId);
        }

        private static string MapToEcOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "1",   // Pending
                "processing" => "2",   // Processing
                "shipped" => "3",   // Shipped
                "delivered" => "5",   // Complete
                "cancelled" => "7",   // Cancelled
                "returned" => "11",  // Returned
                "refunded" => "11",  // Returned/Refunded
                _ => "1"
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(EcProduct p) => new()
        {
            ExternalId = p.ProductId?.ToString() ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku ?? p.Model,
            Price = p.Price,
            StockQuantity = p.Quantity,
            IsActive = p.Status == "1",
            ImageUrl = p.Image,
            Categories = p.Categories?.Select(c => c.Name ?? string.Empty).ToList() ?? [],
            Variants = p.Options?.Select(o => new ExternalProductVariant
            {
                ExternalId = o.ProductOptionId?.ToString() ?? string.Empty,
                Sku = p.Sku,
                Price = p.Price + (o.PricePrefix == "+" ? o.Price : -o.Price),
                Options = new Dictionary<string, string>
                {
                    [o.Name ?? "option"] = o.Value ?? string.Empty
                }
            }).ToList() ?? [],
            UpdatedAt = p.DateModified
        };

        private static ExternalOrder MapToExternalOrder(EcOrder o) => new()
        {
            ExternalId = o.OrderId?.ToString() ?? string.Empty,
            OrderNumber = o.OrderId?.ToString(),
            Status = MapFromEcOrderStatus(o.OrderStatusId?.ToString() ?? "1"),
            TotalAmount = o.Total,
            Currency = o.CurrencyCode ?? "EGP",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId?.ToString() ?? string.Empty,
                Name = $"{o.FirstName} {o.LastName}".Trim(),
                Email = o.Email,
                Phone = o.Telephone
            },
            Items = o.Products?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId?.ToString() ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Sku = i.Model ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                TotalPrice = i.Total
            }).ToList() ?? [],
            ShippingAddress = new ExternalAddress
            {
                Street = o.ShippingAddress1,
                City = o.ShippingCity,
                Country = o.ShippingCountry,
                PostalCode = o.ShippingPostcode
            },
            CreatedAt = o.DateAdded,
            UpdatedAt = o.DateModified
        };

        private static string MapFromEcOrderStatus(string statusId) =>
            statusId switch
            {
                "1" => "pending",
                "2" => "processing",
                "3" => "shipped",
                "5" => "delivered",
                "7" => "cancelled",
                "11" => "returned",
                _ => "pending"
            };
    }

    // ── ExpandCart Egypt API Models ───────────────────────────────────────────

    internal class EcListResponse<T>
    {
        public List<T>? Products { get; set; }
        public List<T>? Orders { get; set; }
        public int? Total { get; set; }
    }

    internal class EcSingleResponse<T>
    {
        public T? Product { get; set; }
        public T? Order { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    internal class EcProduct
    {
        public int? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public string? Model { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Status { get; set; }
        public string? Image { get; set; }
        public DateTime? DateModified { get; set; }
        public List<EcProductCategory>? Categories { get; set; }
        public List<EcProductOption>? Options { get; set; }
    }

    internal class EcProductCategory
    {
        public int? CategoryId { get; set; }
        public string? Name { get; set; }
    }

    internal class EcProductOption
    {
        public int? ProductOptionId { get; set; }
        public string? Name { get; set; }
        public string? Value { get; set; }
        public decimal Price { get; set; }
        public string? PricePrefix { get; set; }  // "+" or "-"
    }

    internal class EcOrder
    {
        public int? OrderId { get; set; }
        public int? CustomerId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public decimal Total { get; set; }
        public string? CurrencyCode { get; set; }
        public int? OrderStatusId { get; set; }
        public string? ShippingAddress1 { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingCountry { get; set; }
        public string? ShippingPostcode { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime? DateModified { get; set; }
        public List<EcOrderProduct>? Products { get; set; }
    }

    internal class EcOrderProduct
    {
        public int? ProductId { get; set; }
        public string? Name { get; set; }
        public string? Model { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }
}