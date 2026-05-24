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

namespace EcomPlatform.Infrastructure.Adapters.AliExpress
{
    /// <summary>
    /// AliExpress Adapter — AliExpress Open Platform (TOP)
    /// Auth: OAuth2 (AliExpress TOP API)
    /// Docs: https://developers.aliexpress.com/en/doc.htm
    /// StoreIntegration:
    ///   ApiKey          = Access Token
    ///   ApiSecret       = App Secret (للـ signing)
    ///   RefreshToken    = Refresh Token
    ///   ExternalStoreId = Seller ID
    /// appsettings:
    ///   AliExpress:AppKey    = App Key
    ///   AliExpress:AppSecret = App Secret
    /// ملاحظة: AliExpress TOP API بيستخدم POST لكل الـ requests
    ///         مع method name كـ parameter مش كـ REST endpoint
    /// </summary>
    public class AliExpressAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://api-sg.aliexpress.com/sync";
        private const string TokenUrl = "https://api-sg.aliexpress.com/rest";

        private readonly HttpClient _httpClient;
        private readonly string _appKey;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.AliExpress;

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

        public AliExpressAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appKey = configuration["AliExpress:AppKey"] ?? string.Empty;
            _appSecret = configuration["AliExpress:AppSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// AliExpress TOP Signature:
        /// 1. Sort all params alphabetically
        /// 2. Concatenate: AppSecret + key1value1key2value2... + AppSecret
        /// 3. HMAC-SHA256 or MD5 (TOP بيدعم الاتنين — بنستخدم HMAC-SHA256)
        /// </summary>
        private string BuildSignature(Dictionary<string, string> parameters)
        {
            var sorted = parameters
                .OrderBy(p => p.Key)
                .Select(p => $"{p.Key}{p.Value}");

            var baseString = $"{_appSecret}{string.Concat(sorted)}{_appSecret}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            return Convert.ToHexString(hash).ToUpper();
        }

        /// <summary>
        /// بيبني الـ common parameters لكل request
        /// AliExpress TOP: كل params بتتبعت كـ form body
        /// </summary>
        private Dictionary<string, string> BuildBaseParams(string method, string accessToken)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            return new Dictionary<string, string>
            {
                ["app_key"] = _appKey,
                ["method"] = method,
                ["access_token"] = accessToken,
                ["timestamp"] = timestamp,
                ["sign_method"] = "sha256",
                ["v"] = "2.0",
                ["format"] = "json"
            };
        }

        /// <summary>
        /// بيعمل signed POST request لـ AliExpress TOP API
        /// </summary>
        private async Task<(bool Success, string Content, int StatusCode)> CallApiAsync(
            string method,
            StoreIntegration integration,
            Dictionary<string, string>? extraParams = null,
            CancellationToken ct = default)
        {
            var parameters = BuildBaseParams(method, integration.ApiKey ?? string.Empty);

            if (extraParams != null)
                foreach (var kv in extraParams)
                    parameters[kv.Key] = kv.Value;

            // Sign بعد إضافة كل الـ params
            parameters["sign"] = BuildSignature(parameters);

            var formContent = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(BaseUrl, formContent, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            return ((int)response.StatusCode < 400, content, (int)response.StatusCode);
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.seller.profile.get",
                    integration,
                    ct: ct);

                if (!success)
                    return AdapterResult.Failure(
                        $"Connection failed: {content}", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressResponse<JsonElement>>(content, _json);

                if (result?.AliexpressSolutionSellerProfileGetResponse != null)
                    return AdapterResult.Success();

                // TOP API بيرجع error_response لو في مشكلة
                if (result?.ErrorResponse != null)
                {
                    if (result.ErrorResponse.Code == "27" || result.ErrorResponse.Code == "token-invalid")
                        return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                    return AdapterResult.Failure(
                        $"Connection failed: {result.ErrorResponse.Msg}",
                        statusCode: statusCode);
                }

                return AdapterResult.Success();
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
                SetAuthHeaders(integration);

                var parameters = new Dictionary<string, string>
                {
                    ["app_key"] = _appKey,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    ["sign_method"] = "sha256",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty
                };

                parameters["sign"] = BuildSignature(parameters);

                var formContent = new FormUrlEncodedContent(parameters);
                var response = await _httpClient.PostAsync(
                    $"{TokenUrl}/auth/token/refresh",
                    formContent, ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<AliExpressTokenResponse>(content, _json);

                if (token == null || string.IsNullOrEmpty(token.AccessToken))
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {token?.Msg ?? content}");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken ?? integration.RefreshToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.AccessTokenExpireTime / 1000)
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

                var currentPage = filter?.Page ?? 1;
                var pageSize = filter?.PageSize ?? 20; // AliExpress max = 20

                var extraParams = new Dictionary<string, string>
                {
                    ["current_page"] = currentPage.ToString(),
                    ["page_size"] = pageSize.ToString(),
                    ["product_status_type"] = "onSelling"
                };

                if (filter?.ModifiedAfter != null)
                {
                    extraParams["gmt_modified_start"] =
                        filter.ModifiedAfter.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    extraParams["gmt_modified_end"] =
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                }

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.product.list.get",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressProductListResponse>(content, _json);
                var items = result?.AliexpressSolutionProductListGetResponse?
                    .Result?.AeItemBaseList?.ItemBaseInfo;

                if (items == null || items.Count == 0)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(
                        new List<ExternalProduct>());

                // جيب التفاصيل لكل منتج (AliExpress list مش بترجع كل التفاصيل)
                var products = new List<ExternalProduct>();
                foreach (var item in items)
                {
                    var detailResult = await GetProductByIdAsync(
                        integration, item.ProductId.ToString(), ct);
                    if (detailResult.IsSuccess && detailResult.Data != null)
                        products.Add(detailResult.Data);
                }

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

                var extraParams = new Dictionary<string, string>
                {
                    ["product_id"] = externalId
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.product.info.get",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressProductDetailResponse>(content, _json);
                var item = result?.AliexpressSolutionProductInfoGetResponse?.Result;

                if (item == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(MapToExternalProduct(item));
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

                // AliExpress: product data بتتبعت كـ JSON string في param
                var productData = new
                {
                    ae_item_base_info_dto = new
                    {
                        subject = product.Name,
                        product_price = product.Price,
                        product_unit = "piece",
                        currency_code = "USD",
                        inventory = new[]
                        {
                            new { quantity = product.StockQuantity }
                        },
                        item_status = product.IsActive ? "onSelling" : "offline"
                    },
                    ae_item_properties = new
                    {
                        list = Array.Empty<object>()
                    },
                    image_u_r_ls = product.ImageUrl != null
                        ? new[] { product.ImageUrl }
                        : Array.Empty<string>()
                };

                var extraParams = new Dictionary<string, string>
                {
                    ["product_id"] = "0", // 0 = create جديد
                    ["ae_item_base_info_dto"] = JsonSerializer.Serialize(
                        productData.ae_item_base_info_dto, _json),
                    ["image_u_r_ls"] = JsonSerializer.Serialize(
                        productData.image_u_r_ls, _json)
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.product.add",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressCreateProductResponse>(content, _json);
                var productId = result?.AliexpressSolutionProductAddResponse?.Result?.ProductId;

                if (productId == null || productId == 0)
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(productId.ToString()!);
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

                var baseInfo = new
                {
                    subject = product.Name,
                    product_price = product.Price,
                    item_status = product.IsActive ? "onSelling" : "offline"
                };

                var extraParams = new Dictionary<string, string>
                {
                    ["product_id"] = product.ExternalId,
                    ["ae_item_base_info_dto"] = JsonSerializer.Serialize(baseInfo, _json)
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.product.update",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult.Failure(
                        $"Failed to update product: {content}", statusCode: statusCode);

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

                // AliExpress: offline المنتج بدل حذفه
                var baseInfo = new { item_status = "offline" };

                var extraParams = new Dictionary<string, string>
                {
                    ["product_id"] = externalId,
                    ["ae_item_base_info_dto"] = JsonSerializer.Serialize(baseInfo, _json)
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.product.update",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult.Failure(
                        $"Failed to delete product: {content}", statusCode: statusCode);

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

                var pageSize = Math.Min(filter?.PageSize ?? 20, 20); // max 20
                var currentPage = filter?.Page ?? 1;

                var extraParams = new Dictionary<string, string>
                {
                    ["page_size"] = pageSize.ToString(),
                    ["current_page_index"] = currentPage.ToString(),
                    ["order_status"] = "ALL"
                };

                if (filter?.ModifiedAfter != null)
                {
                    extraParams["update_date_start"] =
                        filter.ModifiedAfter.Value.ToString("yyyy-MM-dd HH:mm:ss");
                    extraParams["update_date_end"] =
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    // Default: آخر 7 أيام
                    extraParams["create_date_start"] =
                        DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss");
                    extraParams["create_date_end"] =
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                }

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.order.list.get",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressOrderListResponse>(content, _json);
                var orders = result?.AliexpressSolutionOrderListGetResponse?
                    .Result?.OrderList?.OrderDtoList?
                    .Select(MapToExternalOrder).ToList()
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

                var extraParams = new Dictionary<string, string>
                {
                    ["order_id"] = externalId
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.order.get",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: statusCode);

                var result = JsonSerializer.Deserialize<AliExpressOrderDetailResponse>(content, _json);
                var order = result?.AliexpressSolutionOrderGetResponse?.Result;

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

                // AliExpress: بس بيدعم ship_order — مش كل الـ statuses قابلة للتعديل
                if (newStatus.ToLower() != "shipped")
                    return AdapterResult.Failure(
                        $"AliExpress only supports 'shipped' status update via API");

                var extraParams = new Dictionary<string, string>
                {
                    ["order_id"] = externalId,
                    ["logistics_no"] = "N/A",    // tracking number — required
                    ["service_name"] = "OTHER"   // carrier
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.order.fulfill",
                    integration, extraParams, ct);

                if (!success)
                    return AdapterResult.Failure(
                        $"Failed to update order status: {content}", statusCode: statusCode);

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
            var productsResult = await GetProductsAsync(integration, ct: ct);
            if (!productsResult.IsSuccess)
                return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                    productsResult.ErrorMessage ?? "Failed to get inventory");

            var inventory = productsResult.Data?
                .SelectMany(p => p.Variants?.Count > 0
                    ? p.Variants.Select(v => new ExternalInventory
                    {
                        ExternalProductId = v.ExternalId,
                        Sku = v.Sku,
                        Quantity = v.StockQuantity
                    })
                    : new[]
                    {
                        new ExternalInventory
                        {
                            ExternalProductId = p.ExternalId,
                            Sku = p.Sku,
                            Quantity = p.StockQuantity
                        }
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
                    var skuList = new[]
                    {
                        new
                        {
                            sku_code = item.Sku ?? item.ExternalProductId,
                            inventory = item.Quantity,
                            price = 0 // required field — 0 يعني مش بنغير السعر
                        }
                    };

                    var extraParams = new Dictionary<string, string>
                    {
                        ["product_id"] = item.ExternalProductId,
                        ["ae_item_sku_info_dtos"] = JsonSerializer.Serialize(
                            new { ae_item_sku_info_d_t_o = skuList }, _json)
                    };

                    var (success, content, statusCode) = await CallApiAsync(
                        "aliexpress.solution.product.sku.price.stock.update",
                        integration, extraParams, ct);

                    if (!success)
                        errors.Add($"Product {item.ExternalProductId}: {content}");
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

        // ── Webhooks ──────────────────────────────────────────────────────────

        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // AliExpress webhooks بتتسجل عبر الـ TOP API
                var extraParams = new Dictionary<string, string>
                {
                    ["topic"] = "TRADE",  // TRADE = orders
                    ["notify_url"] = $"{integration.StoreUrl}/webhooks/aliexpress"
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.callback.url.update",
                    integration, extraParams, ct);

                return success
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to register webhooks: {content}", statusCode: statusCode);
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

                var extraParams = new Dictionary<string, string>
                {
                    ["topic"] = "TRADE",
                    ["notify_url"] = string.Empty
                };

                var (success, content, statusCode) = await CallApiAsync(
                    "aliexpress.solution.callback.url.update",
                    integration, extraParams, ct);

                return success
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to unregister webhooks: {content}", statusCode: statusCode);
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
            // AliExpress: Signature = MD5(AppSecret + payload + AppSecret)
            var baseString = $"{_appSecret}{payload}{_appSecret}";
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(baseString));
            var expected = Convert.ToHexString(hash).ToUpper();

            return expected == signature.ToUpper();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(AliExpressProductDetail item) => new()
        {
            ExternalId = item.ProductId.ToString(),
            Name = item.Subject ?? string.Empty,
            Description = item.Detail,
            Sku = item.ProductId.ToString(),
            Price = item.AeItemBaseInfoDto?.RegularPrice ?? 0,
            StockQuantity = item.AeItemSkuInfoDtos?.AeItemSkuInfoDto?
                .Sum(s => s.IpmSkuStock) ?? 0,
            IsActive = item.ProductStatusType == "onSelling",
            ImageUrl = item.AeMultimediaInfoDto?.AeVideoInfoDtos == null
                ? item.ImageUrls?.Split(';').FirstOrDefault()
                : null,
            Categories = item.CategoryId != null
                ? [item.CategoryId.ToString()!]
                : [],
            Variants = item.AeItemSkuInfoDtos?.AeItemSkuInfoDto?
                .Select(s => new ExternalProductVariant
                {
                    ExternalId = s.SkuId ?? string.Empty,
                    Sku = s.SkuCode ?? string.Empty,
                    Price = s.OfferSalePrice ?? s.SkuPrice ?? 0,
                    StockQuantity = s.IpmSkuStock,
                    Options = ParseSkuAttr(s.SkuAttr)
                }).ToList() ?? [],
            UpdatedAt = item.GmtModified
        };

        private static ExternalOrder MapToExternalOrder(AliExpressOrder o) => new()
        {
            ExternalId = o.OrderId.ToString(),
            OrderNumber = o.OrderId.ToString(),
            Status = MapFromAliExpressOrderStatus(o.OrderStatus),
            TotalAmount = o.OrderAmount?.Amount ?? 0,
            Currency = o.OrderAmount?.CurrencyCode ?? "USD",
            Customer = o.BuyerInfo == null ? null : new ExternalCustomerInfo
            {
                Name = o.BuyerInfo.BuyerNick,
                Email = o.BuyerInfo.BuyerEmail
            },
            Items = o.ProductList?.ProductSnList?
                .Select(i => new ExternalOrderItem
                {
                    ExternalProductId = i.ProductId.ToString(),
                    ProductName = i.ProductName ?? string.Empty,
                    Sku = i.SkuCode,
                    Quantity = i.ProductCount,
                    UnitPrice = i.ProductUnitPrice?.Amount ?? 0,
                    TotalPrice = (i.ProductUnitPrice?.Amount ?? 0) * i.ProductCount
                }).ToList() ?? [],
            ShippingAddress = o.LogisticsAddress == null ? null : new ExternalAddress
            {
                Street = $"{o.LogisticsAddress.Address} {o.LogisticsAddress.Address2}".Trim(),
                City = o.LogisticsAddress.City,
                Country = o.LogisticsAddress.Country,
                PostalCode = o.LogisticsAddress.Zip
            },
            CreatedAt = o.GmtCreate ?? DateTime.UtcNow,
            UpdatedAt = o.GmtUpdate
        };

        private static string MapFromAliExpressOrderStatus(string? status) =>
            status?.ToUpper() switch
            {
                "PLACE_ORDER_SUCCESS" => "pending",
                "IN_CANCEL" => "cancelled",
                "WAIT_SELLER_SEND_GOODS" => "processing",
                "SELLER_PART_SEND_GOODS" => "processing",
                "WAIT_BUYER_ACCEPT_GOODS" => "shipped",
                "WAIT_GROUP_SUCCESS" => "pending",
                "FINISH" => "delivered",
                "IN_ISSUE" => "disputed",
                "IN_FROZEN" => "on_hold",
                "WAIT_SELLER_EXAMINE_MONEY" => "pending",
                _ => "pending"
            };

        private static Dictionary<string, string> ParseSkuAttr(string? skuAttr)
        {
            // Format: "Color:Red#Size:XL" أو "200000828:Red;200007763:XL"
            if (string.IsNullOrEmpty(skuAttr))
                return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            var parts = skuAttr.Split(';');

            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length >= 2)
                    result[kv[0].Trim()] = kv[1].Trim();
            }

            return result;
        }
    }

    // ── AliExpress API Models ──────────────────────────────────────────────────

    internal class AliExpressResponse<T>
    {
        public AliExpressErrorResponse? ErrorResponse { get; set; }
        public T? AliexpressSolutionSellerProfileGetResponse { get; set; }
    }

    internal class AliExpressErrorResponse
    {
        public string? Code { get; set; }
        public string? Msg { get; set; }
        public string? SubCode { get; set; }
        public string? SubMsg { get; set; }
    }

    internal class AliExpressTokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public long AccessTokenExpireTime { get; set; }
        public long RefreshTokenExpireTime { get; set; }
        public string? Code { get; set; }
        public string? Msg { get; set; }
    }

    // ── Product Models ─────────────────────────────────────────────────────────

    internal class AliExpressProductListResponse
    {
        public AliExpressProductListWrapper? AliexpressSolutionProductListGetResponse { get; set; }
    }

    internal class AliExpressProductListWrapper
    {
        public AliExpressProductListResult? Result { get; set; }
    }

    internal class AliExpressProductListResult
    {
        public AliExpressItemBaseList? AeItemBaseList { get; set; }
        public int TotalPage { get; set; }
        public int TotalCount { get; set; }
    }

    internal class AliExpressItemBaseList
    {
        public List<AliExpressItemRef>? ItemBaseInfo { get; set; }
    }

    internal class AliExpressItemRef
    {
        public long ProductId { get; set; }
        public string? ProductStatusType { get; set; }
    }

    internal class AliExpressProductDetailResponse
    {
        public AliExpressProductDetailWrapper? AliexpressSolutionProductInfoGetResponse { get; set; }
    }

    internal class AliExpressProductDetailWrapper
    {
        public AliExpressProductDetail? Result { get; set; }
    }

    internal class AliExpressProductDetail
    {
        public long ProductId { get; set; }
        public string? Subject { get; set; }
        public string? Detail { get; set; }
        public string? ProductStatusType { get; set; }
        public long? CategoryId { get; set; }
        public string? ImageUrls { get; set; }
        public DateTime? GmtModified { get; set; }
        public AliExpressItemBaseInfoDto? AeItemBaseInfoDto { get; set; }
        public AliExpressSkuInfoList? AeItemSkuInfoDtos { get; set; }
        public AliExpressMultimediaInfo? AeMultimediaInfoDto { get; set; }
    }

    internal class AliExpressItemBaseInfoDto
    {
        public decimal RegularPrice { get; set; }
        public string? CurrencyCode { get; set; }
    }

    internal class AliExpressSkuInfoList
    {
        public List<AliExpressSkuInfo>? AeItemSkuInfoDto { get; set; }
    }

    internal class AliExpressSkuInfo
    {
        public string? SkuId { get; set; }
        public string? SkuCode { get; set; }
        public decimal? SkuPrice { get; set; }
        public decimal? OfferSalePrice { get; set; }
        public int IpmSkuStock { get; set; }
        public string? SkuAttr { get; set; }
    }

    internal class AliExpressMultimediaInfo
    {
        public List<object>? AeVideoInfoDtos { get; set; }
    }

    internal class AliExpressCreateProductResponse
    {
        public AliExpressCreateProductWrapper? AliexpressSolutionProductAddResponse { get; set; }
    }

    internal class AliExpressCreateProductWrapper
    {
        public AliExpressCreateProductResult? Result { get; set; }
    }

    internal class AliExpressCreateProductResult
    {
        public long? ProductId { get; set; }
        public bool Success { get; set; }
    }

    // ── Order Models ───────────────────────────────────────────────────────────

    internal class AliExpressOrderListResponse
    {
        public AliExpressOrderListWrapper? AliexpressSolutionOrderListGetResponse { get; set; }
    }

    internal class AliExpressOrderListWrapper
    {
        public AliExpressOrderListResult? Result { get; set; }
    }

    internal class AliExpressOrderListResult
    {
        public AliExpressOrderList? OrderList { get; set; }
        public int TotalCount { get; set; }
    }

    internal class AliExpressOrderList
    {
        public List<AliExpressOrder>? OrderDtoList { get; set; }
    }

    internal class AliExpressOrderDetailResponse
    {
        public AliExpressOrderDetailWrapper? AliexpressSolutionOrderGetResponse { get; set; }
    }

    internal class AliExpressOrderDetailWrapper
    {
        public AliExpressOrder? Result { get; set; }
    }

    internal class AliExpressOrder
    {
        public long OrderId { get; set; }
        public string? OrderStatus { get; set; }
        public AliExpressMoney? OrderAmount { get; set; }
        public AliExpressBuyerInfo? BuyerInfo { get; set; }
        public AliExpressProductList? ProductList { get; set; }
        public AliExpressLogisticsAddress? LogisticsAddress { get; set; }
        public DateTime? GmtCreate { get; set; }
        public DateTime? GmtUpdate { get; set; }
    }

    internal class AliExpressMoney
    {
        public decimal Amount { get; set; }
        public string? CurrencyCode { get; set; }
    }

    internal class AliExpressBuyerInfo
    {
        public string? BuyerNick { get; set; }
        public string? BuyerEmail { get; set; }
    }

    internal class AliExpressProductList
    {
        public List<AliExpressOrderItem>? ProductSnList { get; set; }
    }

    internal class AliExpressOrderItem
    {
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? SkuCode { get; set; }
        public int ProductCount { get; set; }
        public AliExpressMoney? ProductUnitPrice { get; set; }
    }

    internal class AliExpressLogisticsAddress
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? Zip { get; set; }
        public string? Phone { get; set; }
    }
}