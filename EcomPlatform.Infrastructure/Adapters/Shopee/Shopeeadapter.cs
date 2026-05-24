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

namespace EcomPlatform.Infrastructure.Adapters.Shopee
{
    /// <summary>
    /// Shopee Open Platform Adapter
    /// Auth: OAuth2 + HMAC-SHA256 Signature على كل request
    /// Docs: https://open.shopee.com/documents
    /// StoreIntegration:
    ///   ApiKey        = Access Token
    ///   ApiSecret     = Partner Secret (للـ signing)
    ///   RefreshToken  = Refresh Token
    ///   ExternalStoreId = Shop ID
    /// appsettings:
    ///   Shopee:PartnerId    = Partner ID
    ///   Shopee:PartnerSecret = Partner Secret
    /// </summary>
    public class ShopeeAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://partner.shopeemobile.com/api/v2";

        private readonly HttpClient _httpClient;
        private readonly string _partnerId;
        private readonly string _partnerSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Shopee;

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

        public ShopeeAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _partnerId = configuration["Shopee:PartnerId"] ?? string.Empty;
            _partnerSecret = configuration["Shopee:PartnerSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Shopee Signature = HMAC-SHA256(PartnerSecret, PartnerId + ApiPath + Timestamp + AccessToken + ShopId)
        /// </summary>
        private string BuildSignedUrl(
            string apiPath,
            StoreIntegration integration,
            Dictionary<string, string>? extraParams = null)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var shopId = long.TryParse(integration.ExternalStoreId, out var sid) ? sid : 0;

            var baseString = $"{_partnerId}{apiPath}{timestamp}{integration.ApiKey}{shopId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_partnerSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            var sign = Convert.ToHexString(hash).ToLower();

            var queryParams = new Dictionary<string, string>
            {
                ["partner_id"] = _partnerId,
                ["timestamp"] = timestamp.ToString(),
                ["access_token"] = integration.ApiKey ?? string.Empty,
                ["shop_id"] = shopId.ToString(),
                ["sign"] = sign
            };

            if (extraParams != null)
                foreach (var kv in extraParams)
                    queryParams[kv.Key] = kv.Value;

            var query = string.Join("&",
                queryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

            return $"{BaseUrl}{apiPath}?{query}";
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = BuildSignedUrl("/shop/get_shop_info", integration);
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                if (result?.Error == string.Empty || result?.Error == null)
                    return AdapterResult.Success();

                if (result.Error == "error_auth")
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {result.Message}",
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
                SetAuthHeaders(integration);

                // Shopee refresh token — مش بيحتاج shop_id في الـ signature
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var apiPath = "/auth/access_token/get";
                var baseString = $"{_partnerId}{apiPath}{timestamp}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_partnerSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                var sign = Convert.ToHexString(hash).ToLower();

                var url = $"{BaseUrl}{apiPath}?partner_id={_partnerId}&timestamp={timestamp}&sign={sign}";

                var body = new
                {
                    refresh_token = integration.RefreshToken ?? string.Empty,
                    partner_id = long.Parse(_partnerId),
                    shop_id = long.TryParse(integration.ExternalStoreId, out var sid) ? sid : 0
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeTokenResponse>(content, _json);

                if (result == null || !string.IsNullOrEmpty(result.Error))
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {result?.Message ?? content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
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

                var offset = ((filter?.Page ?? 1) - 1) * (filter?.PageSize ?? 50);
                var pageSize = filter?.PageSize ?? 50;

                var extraParams = new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(),
                    ["page_size"] = pageSize.ToString(),
                    ["item_status"] = "NORMAL"
                };

                if (filter?.ModifiedAfter != null)
                {
                    extraParams["update_time_from"] =
                        DateTimeOffset.Parse(filter.ModifiedAfter.Value.ToString()).ToUnixTimeSeconds().ToString();
                    extraParams["update_time_to"] =
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                }

                // Step 1: جيب الـ item IDs
                var listUrl = BuildSignedUrl("/product/get_item_list", integration, extraParams);
                var listResponse = await _httpClient.GetAsync(listUrl, ct);
                var listContent = await listResponse.Content.ReadAsStringAsync(ct);

                var listResult = JsonSerializer.Deserialize<ShopeeResponse<ShopeeItemListData>>(listContent, _json);

                if (listResult?.Response?.Item == null || listResult.Response.Item.Count == 0)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Success(new List<ExternalProduct>());

                // Step 2: جيب التفاصيل batch
                var itemIds = string.Join(",", listResult.Response.Item.Select(i => i.ItemId));
                var detailUrl = BuildSignedUrl("/product/get_item_base_info", integration,
                    new Dictionary<string, string> { ["item_id_list"] = itemIds });

                var detailResponse = await _httpClient.GetAsync(detailUrl, ct);
                var detailContent = await detailResponse.Content.ReadAsStringAsync(ct);

                var detailResult = JsonSerializer.Deserialize<ShopeeResponse<ShopeeItemDetailData>>(detailContent, _json);

                var products = detailResult?.Response?.ItemList?
                    .Select(MapToExternalProduct).ToList()
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

                var url = BuildSignedUrl("/product/get_item_base_info", integration,
                    new Dictionary<string, string> { ["item_id_list"] = externalId });

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<ShopeeItemDetailData>>(content, _json);
                var item = result?.Response?.ItemList?.FirstOrDefault();

                if (item == null)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

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

                var url = BuildSignedUrl("/product/add_item", integration);

                var body = new
                {
                    original_price = product.Price,
                    description = product.Description ?? string.Empty,
                    item_name = product.Name,
                    normal_stock = product.StockQuantity,
                    weight = 0.5,          // required field — default
                    item_status = product.IsActive ? "NORMAL" : "UNLIST",
                    logistics = new[] { new { logistic_id = 1, enabled = true } },
                    attribute_list = Array.Empty<object>(),
                    category_id = 0,       // سيتحدد لاحقاً
                    image = new
                    {
                        image_url_list = product.ImageUrl != null
                            ? new[] { product.ImageUrl }
                            : Array.Empty<string>()
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<ShopeeCreateItemData>>(content, _json);

                if (!string.IsNullOrEmpty(result?.Error))
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {result.Message}",
                        statusCode: (int)response.StatusCode);

                var itemId = result?.Response?.ItemId.ToString();
                if (string.IsNullOrEmpty(itemId))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(itemId);
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

                var url = BuildSignedUrl("/product/update_item", integration);

                var body = new
                {
                    item_id = long.Parse(product.ExternalId),
                    original_price = product.Price,
                    description = product.Description ?? string.Empty,
                    item_name = product.Name,
                    item_status = product.IsActive ? "NORMAL" : "UNLIST",
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                if (!string.IsNullOrEmpty(result?.Error))
                    return AdapterResult.Failure(
                        $"Failed to update product: {result.Message}",
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

                // Shopee: unlist المنتج بدل حذفه مباشرة
                var url = BuildSignedUrl("/product/unlist_item", integration);

                var body = new
                {
                    item_list = new[]
                    {
                        new { item_id = long.Parse(externalId), unlist = true }
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                if (!string.IsNullOrEmpty(result?.Error))
                    return AdapterResult.Failure(
                        $"Failed to delete product: {result.Message}",
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

                var pageSize = filter?.PageSize ?? 50;
                var timeFrom = filter?.ModifiedAfter != null
                    ? DateTimeOffset.Parse(filter.ModifiedAfter.Value.ToString()).ToUnixTimeSeconds()
                    : DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
                var timeTo = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var extraParams = new Dictionary<string, string>
                {
                    ["time_range_field"] = "update_time",
                    ["time_from"] = timeFrom.ToString(),
                    ["time_to"] = timeTo.ToString(),
                    ["page_size"] = pageSize.ToString(),
                    ["order_status"] = "ALL"
                };

                // Step 1: جيب الـ order numbers
                var listUrl = BuildSignedUrl("/order/get_order_list", integration, extraParams);
                var listResponse = await _httpClient.GetAsync(listUrl, ct);
                var listContent = await listResponse.Content.ReadAsStringAsync(ct);

                var listResult = JsonSerializer.Deserialize<ShopeeResponse<ShopeeOrderListData>>(listContent, _json);

                if (listResult?.Response?.OrderList == null || listResult.Response.OrderList.Count == 0)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Success(new List<ExternalOrder>());

                // Step 2: جيب التفاصيل
                var orderSns = string.Join(",",
                    listResult.Response.OrderList.Select(o => o.OrderSn));

                var detailUrl = BuildSignedUrl("/order/get_order_detail", integration,
                    new Dictionary<string, string>
                    {
                        ["order_sn_list"] = orderSns,
                        ["response_optional_fields"] = "buyer_user_id,buyer_username,estimated_shipping_fee,recipient_address,actual_shipping_fee,goods_to_declare,note,note_update_time,pay_time,dropshipper,dropshipper_phone,split_up,buyer_cancel_reason,cancel_by,cancel_reason,actual_shipping_fee_confirmed,buyer_cpf_id,fulfillment_flag,pickup_done_time,package_list,shipping_carrier,payment_method,total_amount,buyer_username,invoice_data,no_plastic_packing,order_chargeable_weight_gram,edt"
                    });

                var detailResponse = await _httpClient.GetAsync(detailUrl, ct);
                var detailContent = await detailResponse.Content.ReadAsStringAsync(ct);

                var detailResult = JsonSerializer.Deserialize<ShopeeResponse<ShopeeOrderDetailData>>(detailContent, _json);

                var orders = detailResult?.Response?.OrderList?
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

                var url = BuildSignedUrl("/order/get_order_detail", integration,
                    new Dictionary<string, string> { ["order_sn_list"] = externalId });

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<ShopeeOrderDetailData>>(content, _json);
                var order = result?.Response?.OrderList?.FirstOrDefault();

                if (order == null)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

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

                // Shopee: ship_order لتحديث status لـ SHIPPED
                var apiPath = MapToShopeeStatusEndpoint(newStatus);
                if (string.IsNullOrEmpty(apiPath))
                    return AdapterResult.Failure(
                        $"Status '{newStatus}' not directly settable via Shopee API");

                var url = BuildSignedUrl(apiPath, integration);

                var body = new { order_sn = externalId };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                if (!string.IsNullOrEmpty(result?.Error))
                    return AdapterResult.Failure(
                        $"Failed to update order status: {result.Message}",
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

                var url = BuildSignedUrl("/product/update_stock", integration);

                var body = new
                {
                    item_list = items.Select(i => new
                    {
                        item_id = long.TryParse(i.ExternalProductId, out var id) ? id : 0,
                        normal_stock = i.Quantity
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                if (!string.IsNullOrEmpty(result?.Error))
                    return AdapterResult.Failure(
                        $"Failed to update inventory: {result.Message}",
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

                // Shopee webhook URL بيتسجل على مستوى الـ Partner App مش per-shop
                // عن طريق Partner Portal — هنا بنحاول عبر الـ API لو متاح
                var url = BuildSignedUrl("/push/set_app_push_config", integration);

                var body = new
                {
                    callback_url = $"{integration.StoreUrl}/webhooks/shopee",
                    push_config = new
                    {
                        order_status = 1,
                        order_tracking_no = 1,
                        shop_update = 1,
                        item_update = 1,
                        reserved_field_update = 1,
                        banned_item = 1,
                        promotional_event = 1
                    }
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                return string.IsNullOrEmpty(result?.Error)
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to register webhooks: {result.Message}",
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

                var url = BuildSignedUrl("/push/set_app_push_config", integration);

                // نبعت empty callback_url لإلغاء التسجيل
                var body = new { callback_url = string.Empty };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<ShopeeResponse<JsonElement>>(content, _json);

                return string.IsNullOrEmpty(result?.Error)
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to unregister webhooks: {result.Message}",
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
            // Shopee webhook signature = HMAC-SHA256(PartnerSecret, PartnerId + payload)
            var baseString = $"{_partnerId}{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_partnerSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(ShopeeItem item) => new()
        {
            ExternalId = item.ItemId.ToString(),
            Name = item.ItemName ?? string.Empty,
            Description = item.Description,
            Sku = item.ItemSku,
            Price = item.Price,
            StockQuantity = item.Stock,
            IsActive = item.ItemStatus == "NORMAL",
            ImageUrl = item.Image?.ImageUrlList?.FirstOrDefault(),
            Categories = item.CategoryId != 0 ? [item.CategoryId.ToString()] : [],
            Variants = item.ModelList?.Select(m => new ExternalProductVariant
            {
                ExternalId = m.ModelId.ToString(),
                Sku = m.ModelSku,
                Price = m.Price,
                StockQuantity = m.Stock,
                Options = m.TierIndex?
                    .Select((val, idx) => new { Key = $"tier_{idx}", Value = val.ToString() })
                    .ToDictionary(x => x.Key, x => x.Value)
                    ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(item.UpdateTime).UtcDateTime
        };

        private static ExternalOrder MapToExternalOrder(ShopeeOrder o) => new()
        {
            ExternalId = o.OrderSn ?? string.Empty,
            OrderNumber = o.OrderSn ?? string.Empty,
            Status = MapFromShopeeOrderStatus(o.OrderStatus),
            TotalAmount = o.TotalAmount,
            Currency = o.Currency ?? "USD",
            Customer = o.RecipientAddress == null ? null : new ExternalCustomerInfo
            {
                Name = o.RecipientAddress.Name,
                Phone = o.RecipientAddress.Phone
            },
            Items = o.ItemList?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ItemId.ToString(),
                ProductName = i.ItemName ?? string.Empty,
                Sku = i.ItemSku,
                Quantity = i.ModelQuantityPurchased,
                UnitPrice = i.ModelDiscountedPrice,
                TotalPrice = i.ModelDiscountedPrice * i.ModelQuantityPurchased
            }).ToList() ?? [],
            ShippingAddress = o.RecipientAddress == null ? null : new ExternalAddress
            {
                Street = o.RecipientAddress.FullAddress,
                City = o.RecipientAddress.City,
                Country = o.RecipientAddress.State,
                PostalCode = o.RecipientAddress.Zipcode
            },
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(o.CreateTime).UtcDateTime,
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(o.UpdateTime).UtcDateTime
        };

        private static string MapFromShopeeOrderStatus(string? status) =>
            status?.ToUpper() switch
            {
                "UNPAID" => "pending",
                "READY_TO_SHIP" => "processing",
                "PROCESSED" => "processing",
                "SHIPPED" => "shipped",
                "COMPLETED" => "delivered",
                "CANCELLED" => "cancelled",
                "IN_CANCEL" => "cancelled",
                "TO_RETURN" => "returned",
                _ => "pending"
            };

        private static string? MapToShopeeStatusEndpoint(string localStatus) =>
            localStatus.ToLower() switch
            {
                "processing" => "/logistics/ship_order",
                "cancelled" => "/order/cancel_order",
                _ => null
            };
    }

    // ── Shopee API Models ──────────────────────────────────────────────────────

    internal class ShopeeResponse<T>
    {
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? RequestId { get; set; }
        public T? Response { get; set; }
    }

    internal class ShopeeTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    internal class ShopeeItemListData
    {
        public List<ShopeeItemRef>? Item { get; set; }
        public bool HasNextPage { get; set; }
        public int NextOffset { get; set; }
    }

    internal class ShopeeItemRef
    {
        public long ItemId { get; set; }
        public string? ItemStatus { get; set; }
    }

    internal class ShopeeItemDetailData
    {
        public List<ShopeeItem>? ItemList { get; set; }
    }

    internal class ShopeeItem
    {
        public long ItemId { get; set; }
        public string? ItemName { get; set; }
        public string? Description { get; set; }
        public string? ItemSku { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? ItemStatus { get; set; }
        public int CategoryId { get; set; }
        public ShopeeImage? Image { get; set; }
        public List<ShopeeModel>? ModelList { get; set; }
        public long UpdateTime { get; set; }
    }

    internal class ShopeeImage
    {
        public List<string>? ImageUrlList { get; set; }
    }

    internal class ShopeeModel
    {
        public long ModelId { get; set; }
        public string? ModelSku { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public List<int>? TierIndex { get; set; }
    }

    internal class ShopeeCreateItemData
    {
        public long ItemId { get; set; }
    }

    internal class ShopeeOrderListData
    {
        public List<ShopeeOrderRef>? OrderList { get; set; }
        public bool More { get; set; }
    }

    internal class ShopeeOrderRef
    {
        public string? OrderSn { get; set; }
    }

    internal class ShopeeOrderDetailData
    {
        public List<ShopeeOrder>? OrderList { get; set; }
    }

    internal class ShopeeOrder
    {
        public string? OrderSn { get; set; }
        public string? OrderStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Currency { get; set; }
        public ShopeeRecipientAddress? RecipientAddress { get; set; }
        public List<ShopeeOrderItem>? ItemList { get; set; }
        public long CreateTime { get; set; }
        public long UpdateTime { get; set; }
    }

    internal class ShopeeRecipientAddress
    {
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? FullAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zipcode { get; set; }
    }

    internal class ShopeeOrderItem
    {
        public long ItemId { get; set; }
        public string? ItemName { get; set; }
        public string? ItemSku { get; set; }
        public int ModelQuantityPurchased { get; set; }
        public decimal ModelDiscountedPrice { get; set; }
    }
}