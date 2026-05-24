using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.WooCommerce
{
    /// <summary>
    /// WooCommerce REST API Adapter
    /// Auth: Consumer Key + Consumer Secret (Basic Auth)
    /// Docs: https://woocommerce.github.io/woocommerce-rest-api-docs/
    /// APIs used: WooCommerce REST API v3
    /// </summary>
    public class WooCommerceAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.WooCommerce;

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

        public WooCommerceAdapter(HttpClient httpClient, IConfiguration configuration)
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
                SetAuthHeaders(integration);
                var baseUrl = GetBaseUrl(integration);

                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/wp-json/wc/v3/system_status", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid Consumer Key or Secret", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {response.StatusCode}",
                    statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Connection error: {ex.Message}");
            }
        }

        // WooCommerce بيستخدم API Key مش OAuth tokens
        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult<TokenData>.Failure(
                "WooCommerce uses API Key auth, no token refresh needed.", "NOT_SUPPORTED", 501));

        // ── Products ─────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var baseUrl = GetBaseUrl(integration);

                var page = filter?.Page ?? 1;
                var pageSize = filter?.PageSize ?? 100; // WooCommerce max = 100
                var allProducts = new List<ExternalProduct>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{baseUrl}/wp-json/wc/v3/products?page={page}&per_page={pageSize}&status=publish";

                    if (filter?.ModifiedAfter != null)
                        url += $"&modified_after={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ss}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var products = JsonSerializer.Deserialize<List<WooProduct>>(content, _json);
                    if (products is null || products.Count == 0)
                        break;

                    allProducts.AddRange(products.Select(MapToExternalProduct));

                    // WooCommerce بيرجع عدد الصفحات في header
                    var totalPages = GetTotalPages(response);
                    hasMore = page < totalPages && (filter == null || filter.Page == 0);
                    page++;
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
                var baseUrl = GetBaseUrl(integration);

                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/wp-json/wc/v3/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<WooProduct>(content, _json);
                if (product is null)
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
                var baseUrl = GetBaseUrl(integration);

                var body = new
                {
                    name = product.Name,
                    type = "simple",
                    status = product.IsActive ? "publish" : "draft",
                    description = product.Description ?? string.Empty,
                    sku = product.Sku ?? string.Empty,
                    regular_price = product.Price.ToString("F2"),
                    manage_stock = true,
                    stock_quantity = product.StockQuantity,
                    images = product.ImageUrl is not null
                        ? new[] { new { src = product.ImageUrl } }
                        : Array.Empty<object>()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/wp-json/wc/v3/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<WooProduct>(content, _json);
                var id = created?.Id.ToString();

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
                var baseUrl = GetBaseUrl(integration);

                var body = new
                {
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    sku = product.Sku ?? string.Empty,
                    regular_price = product.Price.ToString("F2"),
                    stock_quantity = product.StockQuantity,
                    status = product.IsActive ? "publish" : "draft"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{baseUrl}/wp-json/wc/v3/products/{product.ExternalId}", request, ct);
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
                var baseUrl = GetBaseUrl(integration);

                var response = await _httpClient.DeleteAsync(
                    $"{baseUrl}/wp-json/wc/v3/products/{externalId}?force=true", ct);
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
                var baseUrl = GetBaseUrl(integration);

                var page = filter?.Page ?? 1;
                var pageSize = filter?.PageSize ?? 100;
                var allOrders = new List<ExternalOrder>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{baseUrl}/wp-json/wc/v3/orders?page={page}&per_page={pageSize}";

                    if (filter?.ModifiedAfter != null)
                        url += $"&modified_after={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ss}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var orders = JsonSerializer.Deserialize<List<WooOrder>>(content, _json);
                    if (orders is null || orders.Count == 0)
                        break;

                    allOrders.AddRange(orders.Select(MapToExternalOrder));

                    var totalPages = GetTotalPages(response);
                    hasMore = page < totalPages && (filter == null || filter.Page == 0);
                    page++;
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
                var baseUrl = GetBaseUrl(integration);

                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/wp-json/wc/v3/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<WooOrder>(content, _json);
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
                var baseUrl = GetBaseUrl(integration);

                var body = new { status = MapToWooOrderStatus(newStatus) };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{baseUrl}/wp-json/wc/v3/orders/{externalId}", request, ct);
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
                var baseUrl = GetBaseUrl(integration);

                // WooCommerce Batch Update — max 100 per request
                var errors = new List<string>();
                var batches = items.Chunk(100);

                foreach (var batch in batches)
                {
                    var updateItems = batch.Select(item => new
                    {
                        id = int.TryParse(item.ExternalProductId, out var pid) ? pid : 0,
                        stock_quantity = item.Quantity,
                        manage_stock = true
                    }).ToArray();

                    var body = new { update = updateItems };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{baseUrl}/wp-json/wc/v3/products/batch", request, ct);

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
                var baseUrl = GetBaseUrl(integration);

                var errors = new List<string>();

                // WooCommerce: webhook واحد لكل event
                var wooEvents = new[]
                {
                    "order.created", "order.updated",
                    "product.created", "product.updated", "product.deleted"
                };

                foreach (var evt in wooEvents)
                {
                    var body = new
                    {
                        name = $"EcomPlatform - {evt}",
                        status = "active",
                        topic = evt,
                        delivery_url = integration.WebhookSecret ?? string.Empty
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{baseUrl}/wp-json/wc/v3/webhooks", request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"{evt}: {content}");
                    }
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Some webhooks failed: {string.Join(" | ", errors)}")
                    : AdapterResult.Success();
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
                var baseUrl = GetBaseUrl(integration);

                var listResponse = await _httpClient.GetAsync(
                    $"{baseUrl}/wp-json/wc/v3/webhooks?per_page=100", ct);

                if (!listResponse.IsSuccessStatusCode)
                    return AdapterResult.Success();

                var listContent = await listResponse.Content.ReadAsStringAsync(ct);
                var webhooks = JsonSerializer.Deserialize<List<WooWebhook>>(listContent, _json);

                if (webhooks is null || webhooks.Count == 0)
                    return AdapterResult.Success();

                var errors = new List<string>();
                foreach (var wh in webhooks)
                {
                    var deleteResponse = await _httpClient.DeleteAsync(
                        $"{baseUrl}/wp-json/wc/v3/webhooks/{wh.Id}?force=true", ct);

                    if (!deleteResponse.IsSuccessStatusCode)
                        errors.Add(wh.Id.ToString());
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Failed to delete webhooks: {string.Join(", ", errors)}")
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

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            // WooCommerce: Consumer Key = username, Consumer Secret = password
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{integration.ApiKey}:{integration.ApiSecret}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        private static string GetBaseUrl(StoreIntegration integration)
            => (integration.StoreUrl ?? string.Empty).TrimEnd('/');

        private static int GetTotalPages(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-WP-TotalPages", out var values))
                if (int.TryParse(values.FirstOrDefault(), out var pages))
                    return pages;
            return 1;
        }

        private static string MapToWooOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "pending",
                "processing" => "processing",
                "shipped" => "completed",
                "delivered" => "completed",
                "cancelled" => "cancelled",
                "refunded" => "refunded",
                _ => localStatus.ToLower()
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(WooProduct p) => new()
        {
            ExternalId = p.Id.ToString(),
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = decimal.TryParse(p.Price, out var price) ? price : 0,
            StockQuantity = p.StockQuantity ?? 0,
            IsActive = p.Status == "publish",
            ImageUrl = p.Images?.FirstOrDefault()?.Src,
            Categories = p.Categories?.Select(c => c.Name ?? string.Empty).ToList() ?? [],
            Variants = p.Variations?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.ToString(),
                Sku = string.Empty,
                Price = 0,
                StockQuantity = 0,
                Options = new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.DateModified
        };

        private static ExternalOrder MapToExternalOrder(WooOrder o) => new()
        {
            ExternalId = o.Id.ToString(),
            OrderNumber = o.Number ?? o.Id.ToString(),
            Status = o.Status ?? string.Empty,
            TotalAmount = decimal.TryParse(o.Total, out var total) ? total : 0,
            Currency = o.Currency ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId.ToString(),
                Name = $"{o.Billing?.FirstName} {o.Billing?.LastName}".Trim(),
                Email = o.Billing?.Email ?? string.Empty,
                Phone = o.Billing?.Phone
            },
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId.ToString(),
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku ?? string.Empty,
                Quantity = i.Quantity,
                UnitPrice = decimal.TryParse(i.Price, out var p) ? p : 0,
                TotalPrice = decimal.TryParse(i.Total, out var t) ? t : 0
            }).ToList() ?? [],
            ShippingAddress = o.Shipping is null ? null : new ExternalAddress
            {
                Street = o.Shipping.Address1,
                City = o.Shipping.City,
                Country = o.Shipping.Country,
                PostalCode = o.Shipping.Postcode
            },
            CreatedAt = o.DateCreated ?? DateTime.UtcNow,
            UpdatedAt = o.DateModified
        };
    }

    // ── WooCommerce API Models ─────────────────────────────────────────────────

    // — Products —
    internal class WooProduct
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public string? Price { get; set; }
        public string? RegularPrice { get; set; }
        public bool ManageStock { get; set; }
        public int? StockQuantity { get; set; }
        public List<WooImage>? Images { get; set; }
        public List<WooCategory>? Categories { get; set; }
        public List<int>? Variations { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class WooImage
    {
        public int Id { get; set; }
        public string? Src { get; set; }
        public string? Alt { get; set; }
    }

    internal class WooCategory
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    // — Orders —
    internal class WooOrder
    {
        public int Id { get; set; }
        public string? Number { get; set; }
        public string? Status { get; set; }
        public string? Currency { get; set; }
        public string? Total { get; set; }
        public int CustomerId { get; set; }
        public WooAddress? Billing { get; set; }
        public WooAddress? Shipping { get; set; }
        public List<WooLineItem>? LineItems { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class WooAddress
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Postcode { get; set; }
    }

    internal class WooLineItem
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int ProductId { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public string? Price { get; set; }
        public string? Total { get; set; }
    }

    // — Webhooks —
    internal class WooWebhook
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Topic { get; set; }
        public string? Status { get; set; }
    }
}