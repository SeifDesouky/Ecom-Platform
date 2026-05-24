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

namespace EcomPlatform.Infrastructure.Adapters.BigCommerce
{
    public class BigCommerceAdapter : IMarketplaceAdapter
    {
        // BigCommerce API v2/v3 — store-level base URL is dynamic per store
        private const string ApiVersion = "v3";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.BigCommerce;

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

        public BigCommerceAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["BigCommerce:ClientId"] ?? string.Empty;
            _clientSecret = configuration["BigCommerce:ClientSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// BigCommerce store URL pattern: https://api.bigcommerce.com/stores/{store_hash}/v3
        /// StoreUrl في الـ StoreIntegration بيتحفظ فيه الـ store_hash
        /// ApiKey بيتحفظ فيه الـ Access Token
        /// </summary>
        private string BaseUrl(StoreIntegration integration) =>
            $"https://api.bigcommerce.com/stores/{integration.ExternalStoreId}/{ApiVersion}";

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Auth-Token");
            _httpClient.DefaultRequestHeaders.Remove("X-Auth-Client");
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", integration.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Client", _clientId);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
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
                    $"{BaseUrl(integration)}/catalog/summary", ct);

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
            try
            {
                // BigCommerce OAuth2 — refresh token flow
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var response = await _httpClient.PostAsync(
                    "https://login.bigcommerce.com/oauth2/token", body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<BigCommerceTokenResponse>(content, _json);
                if (token == null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products ──────────────────────────────────────────────────────────

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
                var url = $"{BaseUrl(integration)}/catalog/products?page={page}&limit={pageSize}&include=variants,images";

                if (filter?.ModifiedAfter != null)
                    url += $"&date_modified:min={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var bcResponse = JsonSerializer.Deserialize<BigCommercePagedResponse<BigCommerceProduct>>(content, _json);
                var products = bcResponse?.Data?.Select(MapToExternalProduct).ToList()
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
                    $"{BaseUrl(integration)}/catalog/products/{externalId}?include=variants,images", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var bcResponse = JsonSerializer.Deserialize<BigCommerceSingleResponse<BigCommerceProduct>>(content, _json);
                if (bcResponse?.Data == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(bcResponse.Data));
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
                    type = "physical",
                    sku = product.Sku ?? string.Empty,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    inventory_level = product.StockQuantity,
                    inventory_tracking = "product",
                    is_visible = product.IsActive,
                    variants = product.Variants?.Select(v => new
                    {
                        sku = v.Sku,
                        price = v.Price,
                        inventory_level = v.StockQuantity,
                        option_values = v.Options?.Select(o => new { label = o.Value, option_display_name = o.Key }).ToList()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl(integration)}/catalog/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var bcResponse = JsonSerializer.Deserialize<BigCommerceSingleResponse<BigCommerceProduct>>(content, _json);
                var createdId = bcResponse?.Data?.Id.ToString();

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
                    name = product.Name,
                    sku = product.Sku ?? string.Empty,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    is_visible = product.IsActive,
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl(integration)}/catalog/products/{product.ExternalId}", request, ct);
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
                    $"{BaseUrl(integration)}/catalog/products/{externalId}", ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to delete product: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders ────────────────────────────────────────────────────────────

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
                // Orders API is v2
                var baseV2 = $"https://api.bigcommerce.com/stores/{integration.ExternalStoreId}/v2";
                var url = $"{baseV2}/orders?page={page}&limit={pageSize}&include_items=true";

                if (filter?.ModifiedAfter != null)
                    url += $"&min_date_modified={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var orders = JsonSerializer.Deserialize<List<BigCommerceOrder>>(content, _json)
                    ?? new List<BigCommerceOrder>();

                return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(
                    orders.Select(MapToExternalOrder).ToList());
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

                var baseV2 = $"https://api.bigcommerce.com/stores/{integration.ExternalStoreId}/v2";
                var response = await _httpClient.GetAsync($"{baseV2}/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<BigCommerceOrder>(content, _json);
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
                SetAuthHeaders(integration);

                var body = new { status_id = MapToBigCommerceOrderStatus(newStatus) };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var baseV2 = $"https://api.bigcommerce.com/stores/{integration.ExternalStoreId}/v2";
                var response = await _httpClient.PutAsync(
                    $"{baseV2}/orders/{externalId}", request, ct);
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

        // ── Inventory ─────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = $"{BaseUrl(integration)}/inventory/items?limit=250";
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {content}",
                        statusCode: (int)response.StatusCode);

                var bcResponse = JsonSerializer.Deserialize<BigCommercePagedResponse<BigCommerceInventoryItem>>(content, _json);
                var inventory = bcResponse?.Data?.Select(i => new ExternalInventory
                {
                    ExternalProductId = i.ProductId.ToString(),
                    Sku = i.Sku,
                    Quantity = i.AvailableToSell
                }).ToList() ?? [];

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

                // BigCommerce v3 supports bulk inventory update
                var updates = items.Select(i => new
                {
                    product_id = int.Parse(i.ExternalProductId),
                    quantity = i.Quantity,
                    method = "absolute"
                }).ToList();

                var body = new { items = updates };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl(integration)}/inventory/adjustments/absolute", request, ct);
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

        // ── Webhooks ──────────────────────────────────────────────────────────

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
                        scope = MapToBigCommerceWebhookScope(eventType),
                        destination = $"{integration.StoreUrl}/webhooks/bigcommerce",
                        is_active = true,
                        headers = new { }
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl(integration)}/hooks", request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"{eventType}: {content}");
                    }
                }

                if (errors.Count > 0)
                    return AdapterResult.Failure(
                        $"Some webhooks failed to register: {string.Join(" | ", errors)}");

                return AdapterResult.Success();
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

                // Get all registered webhooks first
                var listResponse = await _httpClient.GetAsync(
                    $"{BaseUrl(integration)}/hooks", ct);
                var listContent = await listResponse.Content.ReadAsStringAsync(ct);

                if (!listResponse.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to list webhooks: {listContent}",
                        statusCode: (int)listResponse.StatusCode);

                var hooks = JsonSerializer.Deserialize<BigCommercePagedResponse<BigCommerceWebhook>>(listContent, _json);
                if (hooks?.Data == null || hooks.Data.Count == 0)
                    return AdapterResult.Success();

                var errors = new List<string>();
                foreach (var hook in hooks.Data)
                {
                    var deleteResponse = await _httpClient.DeleteAsync(
                        $"{BaseUrl(integration)}/hooks/{hook.Id}", ct);

                    if (!deleteResponse.IsSuccessStatusCode)
                        errors.Add($"Hook {hook.Id}: {deleteResponse.StatusCode}");
                }

                if (errors.Count > 0)
                    return AdapterResult.Failure(
                        $"Some webhooks failed to unregister: {string.Join(" | ", errors)}");

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
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(BigCommerceProduct p) => new()
        {
            ExternalId = p.Id.ToString(),
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price,
            StockQuantity = p.InventoryLevel,
            IsActive = p.IsVisible,
            ImageUrl = p.Images?.FirstOrDefault()?.UrlStandard,
            Categories = p.Categories?.Select(c => c.ToString()).ToList() ?? [],
            Variants = p.Variants?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id.ToString(),
                Sku = v.Sku,
                Price = v.Price,
                StockQuantity = v.InventoryLevel,
                Options = v.OptionValues?.ToDictionary(
                    o => o.OptionDisplayName ?? string.Empty,
                    o => o.Label ?? string.Empty)
                    ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.DateModified
        };

        private static ExternalOrder MapToExternalOrder(BigCommerceOrder o) => new()
        {
            ExternalId = o.Id.ToString(),
            OrderNumber = o.Id.ToString(),
            Status = MapFromBigCommerceOrderStatus(o.StatusId),
            TotalAmount = o.TotalIncTax,
            Currency = o.CurrencyCode ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.CustomerId.ToString(),
                Name = $"{o.BillingAddress?.FirstName} {o.BillingAddress?.LastName}".Trim(),
                Email = o.BillingAddress?.Email,
                Phone = o.BillingAddress?.Phone
            },
            Items = o.Products?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId.ToString(),
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.BasePrice,
                TotalPrice = i.TotalIncTax
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddresses?.FirstOrDefault() is { } addr ? new ExternalAddress
            {
                Street = addr.Street1,
                City = addr.City,
                Country = addr.Country,
                PostalCode = addr.Zip
            } : null,
            CreatedAt = o.DateCreated,
            UpdatedAt = o.DateModified
        };

        private static string MapFromBigCommerceOrderStatus(int statusId) =>
            statusId switch
            {
                0 => "pending",
                1 => "pending",        // Awaiting Payment
                2 => "processing",     // Awaiting Fulfillment
                3 => "processing",     // Awaiting Shipment
                4 => "processing",     // Awaiting Pickup
                7 => "cancelled",
                8 => "returned",
                9 => "delivered",
                10 => "shipped",
                11 => "processing",    // Awaiting Fulfillment
                _ => "pending"
            };

        private static int MapToBigCommerceOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => 1,
                "processing" => 2,
                "shipped" => 10,
                "delivered" => 9,
                "cancelled" => 7,
                "returned" => 8,
                _ => 1
            };

        private static string MapToBigCommerceWebhookScope(string eventType) =>
            eventType.ToLower() switch
            {
                "product.created" => "store/product/created",
                "product.updated" => "store/product/updated",
                "product.deleted" => "store/product/deleted",
                "order.created" => "store/order/created",
                "order.updated" => "store/order/updated",
                "order.statusUpdated" => "store/order/statusUpdated",
                "inventory.updated" => "store/sku/inventory/updated",
                _ => eventType
            };
    }

    // ── BigCommerce API Models ─────────────────────────────────────────────────

    internal class BigCommerceTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class BigCommercePagedResponse<T>
    {
        public List<T>? Data { get; set; }
        public BigCommerceMeta? Meta { get; set; }
    }

    internal class BigCommerceSingleResponse<T>
    {
        public T? Data { get; set; }
        public BigCommerceMeta? Meta { get; set; }
    }

    internal class BigCommerceMeta
    {
        public BigCommercePagination? Pagination { get; set; }
    }

    internal class BigCommercePagination
    {
        public int Total { get; set; }
        public int Count { get; set; }
        public int PerPage { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    internal class BigCommerceProduct
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int InventoryLevel { get; set; }
        public bool IsVisible { get; set; }
        public List<int>? Categories { get; set; }
        public List<BigCommerceProductImage>? Images { get; set; }
        public List<BigCommerceVariant>? Variants { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class BigCommerceProductImage
    {
        public int Id { get; set; }
        public string? UrlStandard { get; set; }
        public bool IsThumbnail { get; set; }
    }

    internal class BigCommerceVariant
    {
        public int Id { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int InventoryLevel { get; set; }
        public List<BigCommerceOptionValue>? OptionValues { get; set; }
    }

    internal class BigCommerceOptionValue
    {
        public int Id { get; set; }
        public string? Label { get; set; }
        public string? OptionDisplayName { get; set; }
    }

    internal class BigCommerceOrder
    {
        public int Id { get; set; }
        public int StatusId { get; set; }
        public int CustomerId { get; set; }
        public decimal TotalIncTax { get; set; }
        public string? CurrencyCode { get; set; }
        public BigCommerceBillingAddress? BillingAddress { get; set; }
        public List<BigCommerceShippingAddress>? ShippingAddresses { get; set; }
        public List<BigCommerceOrderProduct>? Products { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
    }

    internal class BigCommerceBillingAddress
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Street1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Zip { get; set; }
    }

    internal class BigCommerceShippingAddress
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Street1 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Zip { get; set; }
    }

    internal class BigCommerceOrderProduct
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal TotalIncTax { get; set; }
    }

    internal class BigCommerceInventoryItem
    {
        public int ProductId { get; set; }
        public string? Sku { get; set; }
        public int AvailableToSell { get; set; }
    }

    internal class BigCommerceWebhook
    {
        public int Id { get; set; }
        public string? Scope { get; set; }
        public string? Destination { get; set; }
        public bool IsActive { get; set; }
    }
}