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

namespace EcomPlatform.Infrastructure.Adapters.Squarespace
{
    public class SquarespaceAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://api.squarespace.com/1.0";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.SquarespaceCommerce;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,   // Squarespace API لا يدعم Customers endpoint مباشرة
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
                SyncEntityType.Inventory,
                SyncEntityType.Prices
            ]
        };

        public SquarespaceAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Squarespace:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Squarespace:ClientSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);
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
                    $"{BaseUrl}/commerce/inventory?cursor=&limit=1", ct);

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
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var response = await _httpClient.PostAsync(
                    "https://login.squarespace.com/api/1/login/oauth/provider/tokens", body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<SquarespaceTokenResponse>(content, _json);
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

                var allProducts = new List<ExternalProduct>();
                string? cursor = null;
                var pageSize = filter?.PageSize ?? 50;

                // Squarespace uses cursor-based pagination
                do
                {
                    var url = $"{BaseUrl}/commerce/products?limit={pageSize}";
                    if (!string.IsNullOrEmpty(cursor))
                        url += $"&cursor={cursor}";

                    if (filter?.ModifiedAfter != null)
                        url += $"&modifiedAfter={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var ssResponse = JsonSerializer.Deserialize<SquarespacePagedResponse<SquarespaceProduct>>(content, _json);
                    if (ssResponse?.Items != null)
                        allProducts.AddRange(ssResponse.Items.Select(MapToExternalProduct));

                    cursor = ssResponse?.Pagination?.NextPageCursor;

                    // إذا filter page محدد نوقف بعد أول page
                    if (filter?.Page > 0) break;

                } while (!string.IsNullOrEmpty(cursor));

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

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/commerce/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<SquarespaceProduct>(content, _json);
                if (product == null)
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

                // بنبني الـ variants list بنوع موحد
                var variantList = product.Variants?.Count > 0
                    ? product.Variants.Select(v => new SquarespaceVariantRequest
                    {
                        Sku = v.Sku ?? product.Sku ?? string.Empty,
                        PriceMoney = new SquarespaceMoneyRequest
                        {
                            Value = ((long)(v.Price * 100)).ToString(),
                            Currency = "USD"
                        },
                        Stock = new SquarespaceStockRequest
                        {
                            Quantity = v.StockQuantity,
                            Unlimited = false
                        },
                        Attributes = v.Options?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>()
                    }).ToList()
                    : new List<SquarespaceVariantRequest>
                    {
                        new()
                        {
                            Sku = product.Sku ?? string.Empty,
                            PriceMoney = new SquarespaceMoneyRequest
                            {
                                Value = ((long)(product.Price * 100)).ToString(),
                                Currency = "USD"
                            },
                            Stock = new SquarespaceStockRequest
                            {
                                Quantity = product.StockQuantity,
                                Unlimited = false
                            },
                            Attributes = new Dictionary<string, string>()
                        }
                    };

                var body = new
                {
                    type = "PHYSICAL",
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    isVisible = product.IsActive,
                    variants = variantList
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/commerce/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var created = JsonSerializer.Deserialize<SquarespaceProduct>(content, _json);
                if (string.IsNullOrEmpty(created?.Id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(created.Id);
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
                    description = product.Description ?? string.Empty,
                    isVisible = product.IsActive,
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/commerce/products/{product.ExternalId}", request, ct);
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
                    $"{BaseUrl}/commerce/products/{externalId}", ct);

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

                var allOrders = new List<ExternalOrder>();
                string? cursor = null;
                var pageSize = filter?.PageSize ?? 50;

                do
                {
                    var url = $"{BaseUrl}/commerce/orders?limit={pageSize}";
                    if (!string.IsNullOrEmpty(cursor))
                        url += $"&cursor={cursor}";

                    if (filter?.ModifiedAfter != null)
                        url += $"&modifiedAfter={filter.ModifiedAfter:yyyy-MM-ddTHH:mm:ssZ}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var ssResponse = JsonSerializer.Deserialize<SquarespacePagedResponse<SquarespaceOrder>>(content, _json);
                    if (ssResponse?.Items != null)
                        allOrders.AddRange(ssResponse.Items.Select(MapToExternalOrder));

                    cursor = ssResponse?.Pagination?.NextPageCursor;

                    if (filter?.Page > 0) break;

                } while (!string.IsNullOrEmpty(cursor));

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

                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/commerce/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<SquarespaceOrder>(content, _json);
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

                // Squarespace: fulfill endpoint لتغيير status لـ FULFILLED
                var ssStatus = MapToSquarespaceOrderStatus(newStatus);

                if (ssStatus == "FULFILLED")
                {
                    var fulfillBody = new { shouldSendNotification = true };
                    var fulfillJson = JsonSerializer.Serialize(fulfillBody, _json);
                    var fulfillRequest = new StringContent(fulfillJson, Encoding.UTF8, "application/json");

                    var fulfillResponse = await _httpClient.PostAsync(
                        $"{BaseUrl}/commerce/orders/{externalId}/fulfillments", fulfillRequest, ct);

                    return fulfillResponse.IsSuccessStatusCode
                        ? AdapterResult.Success()
                        : AdapterResult.Failure(
                            $"Failed to fulfill order: {fulfillResponse.StatusCode}",
                            statusCode: (int)fulfillResponse.StatusCode);
                }

                // للـ statuses التانية
                var body = new { fulfillmentStatus = ssStatus };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/commerce/orders/{externalId}", request, ct);
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

                var allItems = new List<ExternalInventory>();
                string? cursor = null;

                do
                {
                    var url = $"{BaseUrl}/commerce/inventory?limit=100";
                    if (!string.IsNullOrEmpty(cursor))
                        url += $"&cursor={cursor}";

                    var response = await _httpClient.GetAsync(url, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                            $"Failed to get inventory: {content}",
                            statusCode: (int)response.StatusCode);

                    var ssResponse = JsonSerializer.Deserialize<SquarespacePagedResponse<SquarespaceInventoryItem>>(content, _json);
                    if (ssResponse?.Items != null)
                    {
                        allItems.AddRange(ssResponse.Items.Select(i => new ExternalInventory
                        {
                            ExternalProductId = i.VariantId,
                            Sku = i.Sku,
                            Quantity = i.Quantity
                        }));
                    }

                    cursor = ssResponse?.Pagination?.NextPageCursor;

                } while (!string.IsNullOrEmpty(cursor));

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(allItems);
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

                // Squarespace supports batch inventory update
                var updates = items.Select(i => new
                {
                    variantId = i.ExternalProductId,
                    quantity = i.Quantity,
                    isUnlimited = false
                }).ToList();

                var body = new { inventory = updates };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/commerce/inventory/adjustments", request, ct);
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
                        endpointUrl = $"{integration.StoreUrl}/webhooks/squarespace",
                        topics = new[] { MapToSquarespaceWebhookTopic(eventType) }
                    };

                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(
                        $"{BaseUrl}/webhook_subscriptions", request, ct);

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

                var listResponse = await _httpClient.GetAsync(
                    $"{BaseUrl}/webhook_subscriptions", ct);
                var listContent = await listResponse.Content.ReadAsStringAsync(ct);

                if (!listResponse.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to list webhooks: {listContent}",
                        statusCode: (int)listResponse.StatusCode);

                var hooks = JsonSerializer.Deserialize<SquarespacePagedResponse<SquarespaceWebhook>>(listContent, _json);
                if (hooks?.Items == null || hooks.Items.Count == 0)
                    return AdapterResult.Success();

                var errors = new List<string>();
                foreach (var hook in hooks.Items)
                {
                    var deleteResponse = await _httpClient.DeleteAsync(
                        $"{BaseUrl}/webhook_subscriptions/{hook.Id}", ct);

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

            // Squarespace uses HMAC-SHA256
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToBase64String(hash);

            return expected == signature;
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(SquarespaceProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.Variants?.FirstOrDefault()?.Sku,
            Price = p.Variants?.FirstOrDefault()?.PriceMoney?.ValueDecimal ?? 0,
            StockQuantity = p.Variants?.Sum(v => v.Stock?.Quantity ?? 0) ?? 0,
            IsActive = p.IsVisible,
            ImageUrl = p.Images?.FirstOrDefault()?.Url,
            Categories = p.Tags ?? [],
            Variants = p.Variants?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id ?? string.Empty,
                Sku = v.Sku,
                Price = v.PriceMoney?.ValueDecimal ?? 0,
                StockQuantity = v.Stock?.Quantity ?? 0,
                Options = v.Attributes ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.ModifiedOn
        };

        private static ExternalOrder MapToExternalOrder(SquarespaceOrder o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.OrderNumber ?? string.Empty,
            Status = MapFromSquarespaceOrderStatus(o.FulfillmentStatus),
            TotalAmount = o.GrandTotal?.ValueDecimal ?? 0,
            Currency = o.GrandTotal?.Currency ?? "USD",
            Customer = o.CustomerEmail == null ? null : new ExternalCustomerInfo
            {
                Name = $"{o.BillingAddress?.FirstName} {o.BillingAddress?.LastName}".Trim(),
                Email = o.CustomerEmail,
                Phone = o.BillingAddress?.Phone
            },
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId ?? string.Empty,
                ProductName = i.ProductName ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPricePaid?.ValueDecimal ?? 0,
                TotalPrice = (i.UnitPricePaid?.ValueDecimal ?? 0) * i.Quantity
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Address1,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.CountryCode,
                PostalCode = o.ShippingAddress.PostalCode
            },
            CreatedAt = o.CreatedOn,
            UpdatedAt = o.ModifiedOn
        };

        private static string MapFromSquarespaceOrderStatus(string? status) =>
            status?.ToUpper() switch
            {
                "PENDING" => "pending",
                "FULFILLED" => "delivered",
                "CANCELLED" => "cancelled",
                _ => "pending"
            };

        private static string MapToSquarespaceOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "delivered" => "FULFILLED",
                "shipped" => "FULFILLED",
                "cancelled" => "CANCELLED",
                _ => "PENDING"
            };

        private static string MapToSquarespaceWebhookTopic(string eventType) =>
            eventType.ToLower() switch
            {
                "order.created" => "order.create",
                "order.updated" => "order.update",
                "inventory.updated" => "inventory.update",
                _ => eventType
            };
    }

    // ── Squarespace API Models ─────────────────────────────────────────────────

    internal class SquarespaceTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class SquarespacePagedResponse<T>
    {
        public List<T>? Items { get; set; }
        public SquaresspacePagination? Pagination { get; set; }
    }

    internal class SquaresspacePagination
    {
        public string? NextPageCursor { get; set; }
        public bool HasNextPage { get; set; }
    }

    internal class SquarespaceProduct
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsVisible { get; set; }
        public List<string>? Tags { get; set; }
        public List<SquarespaceProductImage>? Images { get; set; }
        public List<SquarespaceVariant>? Variants { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    internal class SquarespaceProductImage
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
    }

    internal class SquarespaceVariant
    {
        public string? Id { get; set; }
        public string? Sku { get; set; }
        public SquaresspaceMoney? PriceMoney { get; set; }
        public SquarespaceStock? Stock { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    internal class SquaresspaceMoney
    {
        public string? Value { get; set; }
        public string? Currency { get; set; }

        // Squarespace بيبعت الـ price كـ string "1999" = 19.99
        public decimal ValueDecimal =>
            decimal.TryParse(Value, out var v) ? v / 100 : 0;
    }

    internal class SquarespaceStock
    {
        public int Quantity { get; set; }
        public bool Unlimited { get; set; }
    }

    internal class SquarespaceOrder
    {
        public string? Id { get; set; }
        public string? OrderNumber { get; set; }
        public string? FulfillmentStatus { get; set; }
        public string? CustomerEmail { get; set; }
        public SquaresspaceMoney? GrandTotal { get; set; }
        public SquarespaceAddress? BillingAddress { get; set; }
        public SquarespaceAddress? ShippingAddress { get; set; }
        public List<SquarespaceLineItem>? LineItems { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }

    internal class SquarespaceAddress
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Address1 { get; set; }
        public string? City { get; set; }
        public string? CountryCode { get; set; }
        public string? PostalCode { get; set; }
    }

    internal class SquarespaceLineItem
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public SquaresspaceMoney? UnitPricePaid { get; set; }
    }

    internal class SquarespaceInventoryItem
    {
        public string VariantId { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public bool IsUnlimited { get; set; }
    }

    internal class SquarespaceWebhook
    {
        public string? Id { get; set; }
        public string? EndpointUrl { get; set; }
        public List<string>? Topics { get; set; }
    }

    // Request models — بنوع ثابت بدل anonymous types متعارضة
    internal class SquarespaceVariantRequest
    {
        public string Sku { get; set; } = string.Empty;
        public SquarespaceMoneyRequest? PriceMoney { get; set; }
        public SquarespaceStockRequest? Stock { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    internal class SquarespaceMoneyRequest
    {
        public string Value { get; set; } = string.Empty;
        public string Currency { get; set; } = "USD";
    }

    internal class SquarespaceStockRequest
    {
        public int Quantity { get; set; }
        public bool Unlimited { get; set; }
    }
}