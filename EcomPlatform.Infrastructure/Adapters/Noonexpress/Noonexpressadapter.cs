using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomPlatform.Infrastructure.Adapters.NoonExpress
{
    /// <summary>
    /// Noon Express Adapter — Noon Fulfillment by Noon (FBN)
    /// Auth: OAuth2 (نفس NoonAdapter — نفس الـ credentials)
    /// Docs: https://developer.noon.com
    /// ملاحظة: Noon Express هو خدمة الـ fulfillment بتاعة Noon
    ///         بيستخدم نفس Noon API لكن endpoints مختلفة خاصة بالـ FBN
    /// StoreIntegration:
    ///   ApiKey          = Access Token
    ///   RefreshToken    = Refresh Token
    ///   ExternalStoreId = Seller ID
    ///   WebhookSecret   = Webhook Signing Secret
    /// appsettings:
    ///   NoonExpress:ClientId     = Client ID
    ///   NoonExpress:ClientSecret = Client Secret
    ///   NoonExpress:Channel      = "egypt" | "uae" | "ksa" (default: egypt)
    /// </summary>
    public class NoonExpressAdapter : IMarketplaceAdapter
    {
        // Noon بيستخدم sub-domains حسب الـ region
        private readonly string _baseUrl;
        private const string EgyptBaseUrl = "https://api.noon.com/seller/v1";
        private const string UaeBaseUrl = "https://api.noon.com/seller/v1";
        private const string KsaBaseUrl = "https://api.noon.com/seller/v1";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _channel;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.NoonExpress;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,
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
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public NoonExpressAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["NoonExpress:ClientId"] ?? string.Empty;
            _clientSecret = configuration["NoonExpress:ClientSecret"] ?? string.Empty;
            _channel = configuration["NoonExpress:Channel"] ?? "egypt";

            _baseUrl = _channel.ToLower() switch
            {
                "uae" => UaeBaseUrl,
                "ksa" => KsaBaseUrl,
                _ => EgyptBaseUrl
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);

            _httpClient.DefaultRequestHeaders.Remove("X-Channel");
            _httpClient.DefaultRequestHeaders.Add("X-Channel", _channel);

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
                    $"{_baseUrl}/seller/profile", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                return AdapterResult.Failure(
                    $"Connection failed: {error?.Message ?? content}",
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
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret
                });

                var response = await _httpClient.PostAsync(
                    "https://api.noon.com/oauth/token", body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<NoonTokenResponse>(content, _json);
                if (token == null || string.IsNullOrEmpty(token.AccessToken))
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
                var url = $"{_baseUrl}/products?page={page}&pageSize={pageSize}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updatedAfter={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<NoonPagedResponse<NoonProduct>>(content, _json);
                var products = result?.Data?.Select(MapToExternalProduct).ToList()
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
                    $"{_baseUrl}/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<NoonSingleResponse<NoonProduct>>(content, _json);
                if (result?.Data == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(result.Data));
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
                    sku = product.Sku ?? product.ExternalId,
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    salePrice = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "ACTIVE" : "INACTIVE",
                    fulfillmentType = "FBN",   // Noon Express = Fulfilled By Noon
                    images = product.ImageUrl != null
                        ? new[] { new { url = product.ImageUrl, isMain = true } }
                        : Array.Empty<object>()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

                var result = JsonSerializer.Deserialize<NoonSingleResponse<NoonProduct>>(content, _json);
                var id = result?.Data?.Id?.ToString();

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
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                    price = product.Price,
                    salePrice = product.Price,
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "ACTIVE" : "INACTIVE"
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}/products/{product.ExternalId}", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to update product: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

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

                // Noon: deactivate بدل حذف
                var body = new { status = "INACTIVE" };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}/products/{externalId}", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to delete product: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

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
                var url = $"{_baseUrl}/orders?page={page}&pageSize={pageSize}";

                if (filter?.ModifiedAfter != null)
                    url += $"&updatedAfter={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<NoonPagedResponse<NoonOrder>>(content, _json);
                var orders = result?.Data?.Select(MapToExternalOrder).ToList()
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
                    $"{_baseUrl}/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<NoonSingleResponse<NoonOrder>>(content, _json);
                if (result?.Data == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(result.Data));
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

                // Noon Express (FBN): الـ fulfillment بيتعمل بواسطة Noon
                // السيلر بيقدر يعمل cancel فقط — shipping بتعملها Noon
                var noonStatus = MapToNoonOrderStatus(newStatus);
                if (noonStatus == null)
                    return AdapterResult.Failure(
                        $"Status '{newStatus}' not supported — Noon Express handles fulfillment automatically");

                var body = new { status = noonStatus };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}/orders/{externalId}/status", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to update order status: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

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

                // Noon Express: inventory خاص بالـ FBN warehouse
                var response = await _httpClient.GetAsync(
                    $"{_baseUrl}/fulfillment/inventory", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                        $"Failed to get inventory: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<NoonPagedResponse<NoonInventoryItem>>(content, _json);

                var inventory = result?.Data?.Select(i => new ExternalInventory
                {
                    ExternalProductId = i.ProductId?.ToString() ?? string.Empty,
                    Sku = i.Sku,
                    Quantity = i.AvailableQuantity
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

                // Noon Express: inventory بيتحدث عبر inbound shipments (FBN)
                // Direct stock update ممكن بس للـ non-FBN items
                var body = new
                {
                    items = items.Select(i => new
                    {
                        sku = i.Sku ?? i.ExternalProductId,
                        quantity = i.Quantity
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{_baseUrl}/inventory/bulk", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to update inventory: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

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

                var body = new
                {
                    url = $"{integration.StoreUrl}/webhooks/noon-express",
                    events = new[]
                    {
                        "order.created",
                        "order.status_changed",
                        "order.cancelled",
                        "fulfillment.status_changed",
                        "inventory.updated",
                        "product.status_changed"
                    },
                    secret = integration.WebhookSecret ?? Guid.NewGuid().ToString("N")
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/webhooks", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to register webhooks: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

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

                var response = await _httpClient.DeleteAsync(
                    $"{_baseUrl}/webhooks", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<NoonErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to unregister webhooks: {error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
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
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(NoonProduct p) => new()
        {
            ExternalId = p.Id?.ToString() ?? string.Empty,
            Name = p.Title ?? string.Empty,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price,
            StockQuantity = p.Quantity,
            IsActive = p.Status?.ToUpper() == "ACTIVE",
            ImageUrl = p.Images?.FirstOrDefault(i => i.IsMain)?.Url
                         ?? p.Images?.FirstOrDefault()?.Url,
            Categories = p.CategoryId != null ? [p.CategoryId.ToString()!] : [],
            Variants = p.Variants?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id?.ToString() ?? string.Empty,
                Sku = v.Sku ?? string.Empty,
                Price = v.Price,
                StockQuantity = v.Quantity,
                Options = v.Attributes?.ToDictionary(
                    a => a.Name ?? string.Empty,
                    a => a.Value ?? string.Empty)
                    ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(NoonOrder o) => new()
        {
            ExternalId = o.Id?.ToString() ?? string.Empty,
            OrderNumber = o.OrderNumber ?? o.Id?.ToString() ?? string.Empty,
            Status = MapFromNoonOrderStatus(o.Status),
            TotalAmount = o.TotalAmount,
            Currency = o.Currency ?? "EGP",
            Customer = o.Customer == null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Customer.Id?.ToString(),
                Name = o.Customer.Name,
                Email = o.Customer.Email,
                Phone = o.Customer.Phone
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId?.ToString() ?? string.Empty,
                ProductName = i.Title ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.UnitPrice * i.Quantity
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Street,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.Country,
                PostalCode = o.ShippingAddress.PostalCode
            },
            CreatedAt = o.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = o.UpdatedAt
        };

        private static string MapFromNoonOrderStatus(string? status) =>
            status?.ToUpper() switch
            {
                "CREATED" => "pending",
                "CONFIRMED" => "processing",
                "PROCESSING" => "processing",
                "SHIPPED" => "shipped",
                "DELIVERED" => "delivered",
                "CANCELLED" => "cancelled",
                "RETURNED" => "returned",
                "REFUNDED" => "returned",
                _ => "pending"
            };

        private static string? MapToNoonOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "cancelled" => "CANCELLED",
                _ => null  // Noon Express handles all other statuses automatically
            };
    }

    // ── Noon Express API Models ────────────────────────────────────────────────

    internal class NoonTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class NoonErrorResponse
    {
        public string? Message { get; set; }
        public string? Code { get; set; }
        public List<string>? Errors { get; set; }
    }

    internal class NoonPagedResponse<T>
    {
        public List<T>? Data { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    internal class NoonSingleResponse<T>
    {
        public T? Data { get; set; }
        public string? Message { get; set; }
    }

    // ── Product Models ─────────────────────────────────────────────────────────

    internal class NoonProduct
    {
        public long? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Status { get; set; }
        public long? CategoryId { get; set; }
        public string? FulfillmentType { get; set; }
        public List<NoonImage>? Images { get; set; }
        public List<NoonVariant>? Variants { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class NoonImage
    {
        public string? Url { get; set; }
        public bool IsMain { get; set; }
    }

    internal class NoonVariant
    {
        public long? Id { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public List<NoonAttribute>? Attributes { get; set; }
    }

    internal class NoonAttribute
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    internal class NoonInventoryItem
    {
        public long? ProductId { get; set; }
        public string? Sku { get; set; }
        public int AvailableQuantity { get; set; }
        public int ReservedQuantity { get; set; }
        public int InboundQuantity { get; set; }
    }

    // ── Order Models ───────────────────────────────────────────────────────────

    internal class NoonOrder
    {
        public long? Id { get; set; }
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Currency { get; set; }
        public NoonCustomer? Customer { get; set; }
        public List<NoonOrderItem>? Items { get; set; }
        public NoonAddress? ShippingAddress { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    internal class NoonCustomer
    {
        public long? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    internal class NoonOrderItem
    {
        public long? ProductId { get; set; }
        public string? Title { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    internal class NoonAddress
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }
}