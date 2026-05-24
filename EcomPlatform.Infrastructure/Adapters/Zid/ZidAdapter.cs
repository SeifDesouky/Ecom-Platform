using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.Zid
{
    public class ZidAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://api.zid.sa/v1";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Zid;

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

        public ZidAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Zid:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Zid:ClientSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/managers/store/info", ct);

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
                // Zid OAuth2 refresh token flow
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var response = await _httpClient.PostAsync(
                    "https://oauth.zid.sa/oauth/token", body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<ZidTokenResponse>(content, _json);
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
                var url = $"{BaseUrl}/managers/products?page={page}&per_page={pageSize}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_after={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var zidResponse = JsonSerializer.Deserialize<ZidListResponse<ZidProduct>>(content, _json);
                var products = zidResponse?.Products?.Select(MapToExternalProduct).ToList()
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
                    $"{BaseUrl}/managers/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var zidResponse = JsonSerializer.Deserialize<ZidSingleResponse<ZidProduct>>(content, _json);
                if (zidResponse?.Product == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(zidResponse.Product));
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
                    name = new { ar = product.Name, en = product.Name },
                    description = new
                    {
                        ar = product.Description ?? string.Empty,
                        en = product.Description ?? string.Empty
                    },
                    sku = product.Sku ?? string.Empty,
                    price = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "active" : "inactive",
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/managers/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var zidResponse = JsonSerializer.Deserialize<ZidSingleResponse<ZidProduct>>(content, _json);
                var createdId = zidResponse?.Product?.Id;

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
                    name = new { ar = product.Name, en = product.Name },
                    description = new
                    {
                        ar = product.Description ?? string.Empty,
                        en = product.Description ?? string.Empty
                    },
                    sku = product.Sku ?? string.Empty,
                    price = product.Price,
                    status = product.IsActive ? "active" : "inactive",
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl}/managers/products/{product.ExternalId}", request, ct);
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
                    $"{BaseUrl}/managers/products/{externalId}", ct);
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
                var url = $"{BaseUrl}/managers/orders?page={page}&per_page={pageSize}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var zidResponse = JsonSerializer.Deserialize<ZidListResponse<ZidOrder>>(content, _json);
                var orders = zidResponse?.Orders?.Select(MapToExternalOrder).ToList()
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
                    $"{BaseUrl}/managers/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var zidResponse = JsonSerializer.Deserialize<ZidSingleResponse<ZidOrder>>(content, _json);
                if (zidResponse?.Order == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(
                    MapToExternalOrder(zidResponse.Order));
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

                var body = new { status = MapToZidOrderStatus(newStatus) };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl}/managers/orders/{externalId}/status", request, ct);
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

                var errors = new List<string>();

                foreach (var item in items)
                {
                    var body = new { quantity = item.Quantity };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PutAsync(
                        $"{BaseUrl}/managers/products/{item.ExternalProductId}/inventory",
                        request, ct);

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

                var body = new { events = eventTypes };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/managers/webhooks", request, ct);

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

                var response = await _httpClient.DeleteAsync(
                    $"{BaseUrl}/managers/webhooks", ct);

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

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);

            // Zid بيتطلب X-Manager-Token في بعض الـ endpoints
            _httpClient.DefaultRequestHeaders.Remove("X-Manager-Token");
            _httpClient.DefaultRequestHeaders.Add("X-Manager-Token", integration.ApiKey);
        }

        private static string MapToZidOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "pending",
                "confirmed" => "confirmed",
                "processing" => "processing",
                "shipped" => "shipped",
                "delivered" => "delivered",
                "cancelled" => "cancelled",
                "returned" => "returned",
                _ => localStatus
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(ZidProduct p) => new()
        {
            ExternalId = p.Id,
            Name = p.Name?.Ar ?? p.Name?.En ?? string.Empty,
            Description = p.Description?.Ar ?? p.Description?.En,
            Sku = p.Sku,
            Price = p.Price,
            StockQuantity = p.Quantity,
            IsActive = p.Status == "active",
            ImageUrl = p.Images?.FirstOrDefault()?.Url,
            Categories = p.Categories?.Select(c => c.Name?.Ar ?? c.Name?.En ?? string.Empty).ToList() ?? [],
            Variants = p.Variants?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id,
                Sku = v.Sku,
                Price = v.Price,
                StockQuantity = v.Quantity,
                Options = v.Options?.ToDictionary(
                    o => o.Name?.Ar ?? o.Name?.En ?? string.Empty,
                    o => o.Value?.Ar ?? o.Value?.En ?? string.Empty)
                    ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(ZidOrder o) => new()
        {
            ExternalId = o.Id,
            OrderNumber = o.Code,
            Status = o.Status ?? string.Empty,
            TotalAmount = o.Total,
            Currency = o.Currency ?? "SAR",
            Customer = o.Customer == null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Customer.Id,
                Name = o.Customer.Name,
                Email = o.Customer.Email,
                Phone = o.Customer.Mobile
            },
            Items = o.Products?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.Id,
                ProductName = i.Name?.Ar ?? i.Name?.En ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.Price,
                TotalPrice = i.Total
            }).ToList() ?? [],
            ShippingAddress = o.Address == null ? null : new ExternalAddress
            {
                Street = o.Address.Street,
                City = o.Address.City,
                Country = o.Address.Country,
                PostalCode = o.Address.ZipCode
            },
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };
    }

    // ── Zid API Models ────────────────────────────────────────────────────────

    internal class ZidTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class ZidListResponse<T>
    {
        public List<T>? Products { get; set; }
        public List<T>? Orders { get; set; }
    }

    internal class ZidSingleResponse<T>
    {
        public T? Product { get; set; }
        public T? Order { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    internal class ZidLocalizedString
    {
        public string? Ar { get; set; }
        public string? En { get; set; }
    }

    internal class ZidProduct
    {
        public string Id { get; set; } = string.Empty;
        public ZidLocalizedString? Name { get; set; }
        public ZidLocalizedString? Description { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Status { get; set; }
        public List<ZidImage>? Images { get; set; }
        public List<ZidCategory>? Categories { get; set; }
        public List<ZidVariant>? Variants { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class ZidImage
    {
        public string? Url { get; set; }
        public bool IsMain { get; set; }
    }

    internal class ZidCategory
    {
        public string? Id { get; set; }
        public ZidLocalizedString? Name { get; set; }
    }

    internal class ZidVariant
    {
        public string Id { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public List<ZidOption>? Options { get; set; }
    }

    internal class ZidOption
    {
        public ZidLocalizedString? Name { get; set; }
        public ZidLocalizedString? Value { get; set; }
    }

    internal class ZidOrder
    {
        public string Id { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Status { get; set; }
        public decimal Total { get; set; }
        public string? Currency { get; set; }
        public ZidCustomer? Customer { get; set; }
        public List<ZidOrderItem>? Products { get; set; }
        public ZidAddress? Address { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class ZidCustomer
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Mobile { get; set; }
    }

    internal class ZidOrderItem
    {
        public string Id { get; set; } = string.Empty;
        public ZidLocalizedString? Name { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

    internal class ZidAddress
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? ZipCode { get; set; }
    }
}