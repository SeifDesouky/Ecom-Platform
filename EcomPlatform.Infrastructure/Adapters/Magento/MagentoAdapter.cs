using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Magento
{
    /// <summary>
    /// Magento 2 REST API Adapter
    /// Auth: Integration Token (Bearer) أو OAuth 1.0a
    /// Docs: https://developer.adobe.com/commerce/webapi/rest/
    /// APIs used: Magento 2 REST API
    /// </summary>
    public class MagentoAdapter : IMarketplaceAdapter
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Magento;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = true,
            SupportsInventory = true,
            SupportsPrices = true,
            SupportsWebhooks = false, // Magento بيستخدم Events/Observers مش webhooks
            SupportsOAuth = false,
            SupportsApiKey = true,   // Integration Token
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

        public MagentoAdapter(HttpClient httpClient, IConfiguration configuration)
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
                    $"{baseUrl}/rest/V1/store/storeConfigs", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid Integration Token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {response.StatusCode}",
                    statusCode: (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Connection error: {ex.Message}");
            }
        }

        // Magento بيستخدم Integration Token ثابت — مش محتاج refresh
        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult<TokenData>.Failure(
                "Magento uses a static Integration Token, no refresh needed.", "NOT_SUPPORTED", 501));

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

                var pageSize = filter?.PageSize ?? 100; // Magento max = 300
                var page = filter?.Page ?? 1;
                var allProducts = new List<ExternalProduct>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{baseUrl}/rest/V1/products" +
                              $"?searchCriteria[pageSize]={pageSize}" +
                              $"&searchCriteria[currentPage]={page}" +
                              $"&searchCriteria[filter_groups][0][filters][0][field]=status" +
                              $"&searchCriteria[filter_groups][0][filters][0][value]=1" +
                              $"&searchCriteria[filter_groups][0][filters][0][condition_type]=eq";

                    if (filter?.ModifiedAfter != null)
                        url += $"&searchCriteria[filter_groups][1][filters][0][field]=updated_at" +
                               $"&searchCriteria[filter_groups][1][filters][0][value]={filter.ModifiedAfter:yyyy-MM-dd HH:mm:ss}" +
                               $"&searchCriteria[filter_groups][1][filters][0][condition_type]=gt";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var magentoResponse = JsonSerializer.Deserialize<MagentoSearchResult<MagentoProduct>>(content, _json);
                    if (magentoResponse?.Items is null || magentoResponse.Items.Count == 0)
                        break;

                    allProducts.AddRange(magentoResponse.Items.Select(MapToExternalProduct));

                    var totalCount = magentoResponse.TotalCount;
                    hasMore = allProducts.Count < totalCount && (filter == null || filter.Page == 0);
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

                // Magento: GET by SKU
                var response = await _httpClient.GetAsync(
                    $"{baseUrl}/rest/V1/products/{Uri.EscapeDataString(externalId)}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<MagentoProduct>(content, _json);
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

                var sku = product.Sku ?? Guid.NewGuid().ToString("N")[..12];

                var body = new
                {
                    product = new
                    {
                        sku = sku,
                        name = product.Name,
                        price = product.Price,
                        status = product.IsActive ? 1 : 2,
                        visibility = 4, // Catalog, Search
                        type_id = "simple",
                        attribute_set_id = 4, // Default attribute set
                        weight = 1,
                        extension_attributes = new
                        {
                            stock_item = new
                            {
                                qty = product.StockQuantity,
                                is_in_stock = product.StockQuantity > 0
                            }
                        },
                        custom_attributes = new[]
                        {
                            new { attribute_code = "description", value = product.Description ?? string.Empty },
                            new { attribute_code = "short_description", value = product.Description ?? string.Empty }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/rest/V1/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<MagentoProduct>(content, _json);
                var id = created?.Sku ?? sku;

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

                var sku = product.Sku ?? product.ExternalId;

                var body = new
                {
                    product = new
                    {
                        name = product.Name,
                        price = product.Price,
                        status = product.IsActive ? 1 : 2,
                        extension_attributes = new
                        {
                            stock_item = new
                            {
                                qty = product.StockQuantity,
                                is_in_stock = product.StockQuantity > 0
                            }
                        },
                        custom_attributes = new[]
                        {
                            new { attribute_code = "description", value = product.Description ?? string.Empty }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{baseUrl}/rest/V1/products/{Uri.EscapeDataString(sku)}", request, ct);
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
                    $"{baseUrl}/rest/V1/products/{Uri.EscapeDataString(externalId)}", ct);
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

                var pageSize = filter?.PageSize ?? 100;
                var page = filter?.Page ?? 1;
                var allOrders = new List<ExternalOrder>();
                var hasMore = true;

                while (hasMore)
                {
                    var url = $"{baseUrl}/rest/V1/orders" +
                              $"?searchCriteria[pageSize]={pageSize}" +
                              $"&searchCriteria[currentPage]={page}";

                    if (filter?.ModifiedAfter != null)
                        url += $"&searchCriteria[filter_groups][0][filters][0][field]=updated_at" +
                               $"&searchCriteria[filter_groups][0][filters][0][value]={filter.ModifiedAfter:yyyy-MM-dd HH:mm:ss}" +
                               $"&searchCriteria[filter_groups][0][filters][0][condition_type]=gt";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var magentoResponse = JsonSerializer.Deserialize<MagentoSearchResult<MagentoOrder>>(content, _json);
                    if (magentoResponse?.Items is null || magentoResponse.Items.Count == 0)
                        break;

                    allOrders.AddRange(magentoResponse.Items.Select(MapToExternalOrder));

                    var totalCount = magentoResponse.TotalCount;
                    hasMore = allOrders.Count < totalCount && (filter == null || filter.Page == 0);
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
                    $"{baseUrl}/rest/V1/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<MagentoOrder>(content, _json);
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

                var body = new
                {
                    entity = new
                    {
                        entity_id = int.TryParse(externalId, out var eid) ? eid : 0,
                        status = MapToMagentoOrderStatus(newStatus),
                        state = MapToMagentoOrderState(newStatus)
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/rest/V1/orders", request, ct);
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

                var errors = new List<string>();

                // Magento: Bulk Source Items API
                var sourceItems = items.Select(item => new
                {
                    sku = item.Sku ?? item.ExternalProductId,
                    source_code = "default",
                    quantity = (double)item.Quantity,
                    status = item.Quantity > 0 ? 1 : 0
                }).ToArray();

                var body = new { sourceItems };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{baseUrl}/rest/V1/inventory/source-items", request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    errors.Add(content);
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Inventory update failed: {string.Join(" | ", errors)}")
                    : AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks ─────────────────────────────────────────────────────────
        // Magento بيستخدم Events/Observers — مش REST webhooks

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Failure(
                "Magento uses server-side Events/Observers, not REST webhooks. Use polling instead.",
                "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(AdapterResult.Success());

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
            => false;

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);
        }

        private static string GetBaseUrl(StoreIntegration integration)
            => (integration.StoreUrl ?? string.Empty).TrimEnd('/');

        private static string MapToMagentoOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "pending",
                "processing" => "processing",
                "shipped" => "complete",
                "delivered" => "complete",
                "cancelled" => "canceled",
                _ => localStatus.ToLower()
            };

        private static string MapToMagentoOrderState(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "new",
                "processing" => "processing",
                "shipped" => "complete",
                "delivered" => "complete",
                "cancelled" => "canceled",
                _ => "processing"
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(MagentoProduct p)
        {
            var description = p.CustomAttributes?
                .FirstOrDefault(a => a.AttributeCode == "description")?.Value;

            var imageUrl = p.CustomAttributes?
                .FirstOrDefault(a => a.AttributeCode == "thumbnail")?.Value;

            return new ExternalProduct
            {
                ExternalId = p.Sku ?? string.Empty,
                Name = p.Name ?? string.Empty,
                Description = description,
                Sku = p.Sku,
                Price = p.Price,
                StockQuantity = p.ExtensionAttributes?.StockItem?.Qty ?? 0,
                IsActive = p.Status == 1,
                ImageUrl = imageUrl is not null
                    ? $"/media/catalog/product{imageUrl}"
                    : null,
                Categories = [],
                Variants = [],
                UpdatedAt = p.UpdatedAt
            };
        }

        private static ExternalOrder MapToExternalOrder(MagentoOrder o) => new()
        {
            ExternalId = o.EntityId.ToString(),
            OrderNumber = o.IncrementId ?? o.EntityId.ToString(),
            Status = o.Status ?? string.Empty,
            TotalAmount = o.GrandTotal,
            Currency = o.OrderCurrencyCode ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId?.ToString() ?? string.Empty,
                Name = $"{o.CustomerFirstname} {o.CustomerLastname}".Trim(),
                Email = o.CustomerEmail ?? string.Empty
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId.ToString(),
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku ?? string.Empty,
                Quantity = (int)(i.QtyOrdered ?? 0),
                UnitPrice = i.Price,
                TotalPrice = i.RowTotal
            }).ToList() ?? [],
            ShippingAddress = o.ExtensionAttributes?.ShippingAssignments?
                .FirstOrDefault()?.Shipping?.Address is MagentoAddress addr
                ? new ExternalAddress
                {
                    Street = addr.Street?.FirstOrDefault(),
                    City = addr.City,
                    Country = addr.CountryId,
                    PostalCode = addr.Postcode
                }
                : null,
            CreatedAt = o.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = o.UpdatedAt
        };
    }

    // ── Magento API Models ────────────────────────────────────────────────────

    internal class MagentoSearchResult<T>
    {
        public List<T>? Items { get; set; }
        public int TotalCount { get; set; }
    }

    // — Products —
    internal class MagentoProduct
    {
        public int Id { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Status { get; set; }
        public int Visibility { get; set; }
        public string? TypeId { get; set; }
        public MagentoProductExtension? ExtensionAttributes { get; set; }
        public List<MagentoCustomAttribute>? CustomAttributes { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class MagentoProductExtension
    {
        public MagentoStockItem? StockItem { get; set; }
    }

    internal class MagentoStockItem
    {
        public int ItemId { get; set; }
        public int Qty { get; set; }
        public bool IsInStock { get; set; }
    }

    internal class MagentoCustomAttribute
    {
        public string? AttributeCode { get; set; }
        public string? Value { get; set; }
    }

    // — Orders —
    internal class MagentoOrder
    {
        public int EntityId { get; set; }
        public string? IncrementId { get; set; }
        public string? Status { get; set; }
        public string? State { get; set; }
        public decimal GrandTotal { get; set; }
        public string? OrderCurrencyCode { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerFirstname { get; set; }
        public string? CustomerLastname { get; set; }
        public List<MagentoOrderItem>? Items { get; set; }
        public MagentoOrderExtension? ExtensionAttributes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class MagentoOrderItem
    {
        public int ItemId { get; set; }
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public decimal? QtyOrdered { get; set; }
        public decimal Price { get; set; }
        public decimal RowTotal { get; set; }
    }

    internal class MagentoOrderExtension
    {
        public List<MagentoShippingAssignment>? ShippingAssignments { get; set; }
    }

    internal class MagentoShippingAssignment
    {
        public MagentoShipping? Shipping { get; set; }
    }

    internal class MagentoShipping
    {
        public MagentoAddress? Address { get; set; }
    }

    internal class MagentoAddress
    {
        public List<string>? Street { get; set; }
        public string? City { get; set; }
        public string? CountryId { get; set; }
        public string? Postcode { get; set; }
        public string? Telephone { get; set; }
    }
}