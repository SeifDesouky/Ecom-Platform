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

namespace EcomPlatform.Infrastructure.Adapters.TikTokShop
{
    /// <summary>
    /// TikTok Shop API v202309
    /// Docs: https://partner.tiktokshop.com/docv2/page/650aa4dd4a0bb702c7d3e2a2
    /// Auth: OAuth2 — كل request محتاج HMAC-SHA256 signature
    /// </summary>
    public class TikTokShopAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://open-api.tiktokglobalshop.com";

        private readonly HttpClient _httpClient;
        private readonly string _appKey;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.TikTokShop;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,  // TikTok مش بيكشف customer data كاملة
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

        public TikTokShopAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appKey = configuration["TikTokShop:AppKey"] ?? string.Empty;
            _appSecret = configuration["TikTokShop:AppSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var path = "/authorization/202309/shops";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Connection failed: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokBaseResponse>(content, _json);

                return result?.Code == 0
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(result?.Message ?? "Connection failed", "TIKTOK_ERROR");
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
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var url = $"{BaseUrl}/api/v2/token/refresh" +
                          $"?app_key={_appKey}" +
                          $"&refresh_token={integration.RefreshToken}" +
                          $"&grant_type=refresh_token" +
                          $"&timestamp={timestamp}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<TikTokTokenResponse>(content, _json);
                if (token?.Data == null)
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.Data.AccessToken,
                    RefreshToken = token.Data.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.Data.AccessTokenExpireIn)
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
                var pageSize = filter?.PageSize ?? 20; // TikTok max = 100
                var path = "/product/202309/products/search";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty,
                    $"page_size={pageSize}");

                var body = new
                {
                    page_token = filter?.Cursor ?? string.Empty,
                    update_time_from = filter?.ModifiedAfter.HasValue == true
                        ? new DateTimeOffset(filter.ModifiedAfter.Value).ToUnixTimeSeconds()
                        : (long?)null
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokResponse<TikTokProductsData>>(content, _json);
                if (result?.Code != 0)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        result?.Message ?? "Failed to get products");

                var products = result.Data?.Products?.Select(MapToExternalProduct).ToList()
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
                var path = $"/product/202309/products/{externalId}";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokResponse<TikTokProductDetail>>(content, _json);
                if (result?.Code != 0 || result.Data == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapDetailToExternalProduct(result.Data));
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
                var path = "/product/202309/products";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var body = new
                {
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                    skus = new[]
                    {
                        new
                        {
                            seller_sku    = product.Sku ?? string.Empty,
                            original_price = product.Price.ToString("F2"),
                            stock_infos   = new[] { new { available_stock = product.StockQuantity } }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokResponse<TikTokCreateProductData>>(content, _json);
                if (result?.Code != 0 || result.Data == null)
                    return AdapterResult<string>.Failure(result?.Message ?? "Product created but ID not returned");

                return AdapterResult<string>.Success(result.Data.ProductId);
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
                var path = $"/product/202309/products/{product.ExternalId}";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var body = new
                {
                    title = product.Name,
                    description = product.Description ?? string.Empty,
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, request, ct);

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
                var path = "/product/202309/products";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var body = new { product_ids = new[] { externalId } };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new HttpRequestMessage(HttpMethod.Delete, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request, ct);

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
                var pageSize = filter?.PageSize ?? 20;
                var path = "/order/202309/orders/search";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty,
                    $"page_size={pageSize}");

                var body = new
                {
                    page_token = filter?.Cursor ?? string.Empty,
                    update_time_from = filter?.ModifiedAfter.HasValue == true
                        ? new DateTimeOffset(filter.ModifiedAfter.Value).ToUnixTimeSeconds()
                        : (long?)null,
                    update_time_to = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokResponse<TikTokOrdersData>>(content, _json);
                if (result?.Code != 0)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        result?.Message ?? "Failed to get orders");

                var orders = result.Data?.Orders?.Select(MapToExternalOrder).ToList()
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
                var path = $"/order/202309/orders/{externalId}";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<TikTokResponse<TikTokOrderDetail>>(content, _json);
                if (result?.Code != 0 || result.Data == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(MapDetailToExternalOrder(result.Data));
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
                // TikTok بيستخدم ship/confirm endpoints مش status update مباشر
                var tikTokStatus = MapToTikTokOrderAction(newStatus);
                if (tikTokStatus == null)
                    return AdapterResult.Failure($"Unsupported status: {newStatus}", "NOT_SUPPORTED");

                var path = $"/fulfillment/202309/orders/{externalId}/{tikTokStatus}";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var response = await _httpClient.PostAsync(
                    url, new StringContent("{}", Encoding.UTF8, "application/json"), ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update order: {response.StatusCode}",
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
                }).ToList() ?? [];

            return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(inventory);
        }

        public async Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
        {
            try
            {
                var path = "/product/202309/inventory";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var body = new
                {
                    skus = items.Select(i => new
                    {
                        id = i.ExternalProductId,
                        stock_infos = new[] { new { available_stock = i.Quantity } }
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, request, ct);

                return response.IsSuccessStatusCode
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to update inventory: {response.StatusCode}",
                        statusCode: (int)response.StatusCode);
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
                var path = "/event/202309/webhooks";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);

                var body = new
                {
                    address = "https://rahtk.sa/api/webhooks/tiktok",
                    event_types = eventTypes.Select(MapToTikTokEventType).ToList()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");
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
                var path = "/event/202309/webhooks";
                var url = BuildSignedUrl(path, integration.ApiKey ?? string.Empty);
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
            if (string.IsNullOrEmpty(_appSecret)) return false;

            // TikTok: HMAC-SHA256 على الـ timestamp + payload
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = Convert.ToHexString(hash).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
        }

        // ── Signature Builder ─────────────────────────────────────────────────

        /// <summary>
        /// TikTok كل request محتاج HMAC-SHA256 signature بالـ app_key + timestamp + path
        /// Docs: https://partner.tiktokshop.com/docv2/page/650aa58bcfe6e02f1b474487
        /// </summary>
        private string BuildSignedUrl(
            string path,
            string accessToken,
            string? extraParams = null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // بناء الـ string to sign: secret + path + params + timestamp + secret
            var paramStr = $"app_key={_appKey}&timestamp={timestamp}";
            if (!string.IsNullOrEmpty(extraParams))
                paramStr += $"&{extraParams}";

            var toSign = $"{_appSecret}{path}{paramStr}{_appSecret}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var sign = Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign))).ToLowerInvariant();

            var url = $"{BaseUrl}{path}?{paramStr}&sign={sign}&access_token={accessToken}";
            return url;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string? MapToTikTokOrderAction(string status) =>
            status.ToLower() switch
            {
                "shipped" => "ship",
                "confirmed" => "confirm",
                _ => null
            };

        private static string MapToTikTokEventType(string eventType) =>
            eventType switch
            {
                "order.created" => "ORDER_STATUS_CHANGE",
                "order.updated" => "ORDER_STATUS_CHANGE",
                "product.created" => "PRODUCT_STATUS_CHANGE",
                "product.updated" => "PRODUCT_STATUS_CHANGE",
                "inventory.updated" => "PRODUCT_STATUS_CHANGE",
                _ => eventType
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(TikTokProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Title ?? string.Empty,
            IsActive = p.Status == "ACTIVATE",
            UpdatedAt = p.UpdateTime.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(p.UpdateTime.Value).UtcDateTime
                : null
        };

        private static ExternalProduct MapDetailToExternalProduct(TikTokProductDetail p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Title ?? string.Empty,
            Description = p.Description,
            Sku = p.Skus?.FirstOrDefault()?.SellerSku,
            Price = p.Skus?.FirstOrDefault()?.Price?.OriginalPrice ?? 0,
            StockQuantity = p.Skus?.Sum(s =>
                s.StockInfos?.Sum(si => si.AvailableStock) ?? 0) ?? 0,
            IsActive = p.Status == "ACTIVATE",
        };

        private static ExternalOrder MapToExternalOrder(TikTokOrder o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.Id ?? string.Empty,
            Status = o.Status ?? string.Empty,
            TotalAmount = o.PaymentInfo?.TotalAmount ?? 0,
            Currency = o.Currency ?? "USD",
            CreatedAt = o.CreateTime.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(o.CreateTime.Value).UtcDateTime
                : DateTime.UtcNow,
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId ?? string.Empty,
                ProductName = i.ProductName ?? string.Empty,
                Sku = i.SellerSku,
                Quantity = i.Quantity,
                UnitPrice = i.OriginalPrice,
                TotalPrice = i.OriginalPrice * i.Quantity
            }).ToList() ?? []
        };

        private static ExternalOrder MapDetailToExternalOrder(TikTokOrderDetail o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.Id ?? string.Empty,
            Status = o.Status ?? string.Empty,
            TotalAmount = o.PaymentInfo?.TotalAmount ?? 0,
            Currency = o.Currency ?? "USD",
            ShippingAddress = o.RecipientAddress == null ? null : new ExternalAddress
            {
                Street = o.RecipientAddress.AddressLine1,
                City = o.RecipientAddress.City,
                Country = o.RecipientAddress.RegionCode,
                PostalCode = o.RecipientAddress.PostalCode,
                Phone = o.RecipientAddress.Phone
            },
            Items = o.LineItems?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId ?? string.Empty,
                ProductName = i.ProductName ?? string.Empty,
                Sku = i.SellerSku,
                Quantity = i.Quantity,
                UnitPrice = i.OriginalPrice,
                TotalPrice = i.OriginalPrice * i.Quantity
            }).ToList() ?? [],
            CreatedAt = o.CreateTime.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(o.CreateTime.Value).UtcDateTime
                : DateTime.UtcNow
        };
    }

    // ── TikTok API Models ─────────────────────────────────────────────────────

    internal class TikTokBaseResponse
    {
        public int Code { get; set; }
        public string? Message { get; set; }
    }

    internal class TikTokResponse<T> : TikTokBaseResponse
    {
        public T? Data { get; set; }
    }

    internal class TikTokTokenResponse
    {
        public int Code { get; set; }
        public TikTokTokenData? Data { get; set; }
    }

    internal class TikTokTokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public long AccessTokenExpireIn { get; set; }
        public long RefreshTokenExpireIn { get; set; }
    }

    internal class TikTokProductsData
    {
        public List<TikTokProduct>? Products { get; set; }
        public string? NextPageToken { get; set; }
        public int? TotalCount { get; set; }
    }

    internal class TikTokProduct
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public long? UpdateTime { get; set; }
    }

    internal class TikTokProductDetail
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public List<TikTokSku>? Skus { get; set; }
    }

    internal class TikTokSku
    {
        public string? Id { get; set; }
        public string? SellerSku { get; set; }
        public TikTokPrice? Price { get; set; }
        public List<TikTokStockInfo>? StockInfos { get; set; }
    }

    internal class TikTokPrice
    {
        public decimal OriginalPrice { get; set; }
        public decimal SalePrice { get; set; }
        public string? Currency { get; set; }
    }

    internal class TikTokStockInfo
    {
        public int AvailableStock { get; set; }
        public string? WarehouseId { get; set; }
    }

    internal class TikTokCreateProductData
    {
        public string ProductId { get; set; } = string.Empty;
    }

    internal class TikTokOrdersData
    {
        public List<TikTokOrder>? Orders { get; set; }
        public string? NextPageToken { get; set; }
        public int? TotalCount { get; set; }
    }

    internal class TikTokOrder
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public string? Currency { get; set; }
        public long? CreateTime { get; set; }
        public TikTokPaymentInfo? PaymentInfo { get; set; }
        public List<TikTokLineItem>? LineItems { get; set; }
    }

    internal class TikTokOrderDetail : TikTokOrder
    {
        public TikTokAddress? RecipientAddress { get; set; }
    }

    internal class TikTokPaymentInfo
    {
        public decimal TotalAmount { get; set; }
        public string? Currency { get; set; }
    }

    internal class TikTokLineItem
    {
        public string? Id { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? SellerSku { get; set; }
        public int Quantity { get; set; }
        public decimal OriginalPrice { get; set; }
    }

    internal class TikTokAddress
    {
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? RegionCode { get; set; }
        public string? PostalCode { get; set; }
        public string? Phone { get; set; }
    }
}