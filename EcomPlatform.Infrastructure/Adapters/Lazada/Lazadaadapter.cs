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
using System.Web;

namespace EcomPlatform.Infrastructure.Adapters.Lazada
{
    /// <summary>
    /// Lazada Open Platform Adapter
    /// Auth: OAuth2 — Access Token + App Key + HMAC-SHA256 Signature على كل request
    /// Docs: https://open.lazada.com/apps/doc/doc.htm
    /// StoreIntegration:
    ///   ApiKey       = Access Token
    ///   ApiSecret    = App Secret (للـ signing)
    ///   RefreshToken = Refresh Token
    ///   ExternalStoreId = Seller ID / Country code (e.g. "SG", "MY", "TH")
    /// </summary>
    public class LazadaAdapter : IMarketplaceAdapter
    {
        // Lazada API endpoint بيتغير حسب الـ region
        private static readonly Dictionary<string, string> _regionBaseUrls = new()
        {
            ["SG"] = "https://api.lazada.com.sg/rest",
            ["MY"] = "https://api.lazada.com.my/rest",
            ["TH"] = "https://api.lazada.co.th/rest",
            ["PH"] = "https://api.lazada.com.ph/rest",
            ["ID"] = "https://api.lazada.co.id/rest",
            ["VN"] = "https://api.lazada.vn/rest",
        };
        private const string DefaultBaseUrl = "https://api.lazada.com/rest";
        private const string AuthUrl = "https://auth.lazada.com/rest";

        private readonly HttpClient _httpClient;
        private readonly string _appKey;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Lazada;

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

        public LazadaAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appKey = configuration["Lazada:AppKey"] ?? string.Empty;
            _appSecret = configuration["Lazada:AppSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetBaseUrl(StoreIntegration integration)
        {
            var region = integration.ExternalStoreId?.ToUpper() ?? string.Empty;
            return _regionBaseUrls.TryGetValue(region, out var url) ? url : DefaultBaseUrl;
        }

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Lazada بيطلب HMAC-SHA256 signature على كل API call
        /// Sign = HMAC-SHA256(AppSecret, Method + SortedParams)
        /// </summary>
        private string BuildSignedUrl(
            string baseUrl,
            string apiPath,
            Dictionary<string, string> parameters,
            StoreIntegration integration)
        {
            // أضف الـ common params
            parameters["app_key"] = _appKey;
            parameters["access_token"] = integration.ApiKey ?? string.Empty;
            parameters["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            parameters["sign_method"] = "sha256";

            // Sort params alphabetically
            var sorted = parameters.OrderBy(p => p.Key);
            var paramString = string.Concat(sorted.Select(p => p.Key + p.Value));

            // Sign = HMAC-SHA256(AppSecret, ApiPath + ParamString)
            var signInput = apiPath + paramString;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signInput));
            parameters["sign"] = Convert.ToHexString(hash).ToUpper();

            // Build URL
            var query = string.Join("&",
                parameters.Select(p =>
                    $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}"));

            return $"{baseUrl}{apiPath}?{query}";
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/seller/get",
                    new Dictionary<string, string>(),
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                if (result?.Code == "0" || result?.Code == null && response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (result?.Code == "27")
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {result?.Message ?? response.StatusCode.ToString()}",
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

                var url = BuildSignedUrl(
                    AuthUrl,
                    "/auth/token/refresh",
                    new Dictionary<string, string>
                    {
                        ["refresh_token"] = integration.RefreshToken ?? string.Empty
                    },
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaTokenData>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {result?.Message ?? content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = result.Data.AccessToken,
                    RefreshToken = result.Data.RefreshToken,
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(result.Data.ExpiresIn).UtcDateTime
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
                var limit = filter?.PageSize ?? 50;

                var parameters = new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(),
                    ["limit"] = limit.ToString(),
                    ["filter"] = "all"
                };

                if (filter?.ModifiedAfter != null)
                    parameters["update_after"] = filter.ModifiedAfter.Value.ToString("yyyy-MM-dd HH:mm:ss");

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/products/get",
                    parameters,
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaProductsData>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {result?.Message ?? content}",
                        statusCode: (int)response.StatusCode);

                var products = result.Data.Products?.Select(MapToExternalProduct).ToList()
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

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/product/item/get",
                    new Dictionary<string, string> { ["item_id"] = externalId },
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaProductItem>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(result.Data));
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

                // Lazada بيستخدم XML payload لإنشاء المنتجات
                var xmlPayload = BuildProductXml(product);

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/product/create",
                    new Dictionary<string, string> { ["payload"] = xmlPayload },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaCreateProductData>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {result?.Message ?? content}",
                        statusCode: (int)response.StatusCode);

                var itemId = result.Data.ItemId?.ToString();
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

                var xmlPayload = BuildProductXml(product, isUpdate: true);

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/product/update",
                    new Dictionary<string, string> { ["payload"] = xmlPayload },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                if (result?.Code != "0")
                    return AdapterResult.Failure(
                        $"Failed to update product: {result?.Message ?? content}",
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

                // Lazada Delete تاخد list من الـ item IDs
                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/product/remove",
                    new Dictionary<string, string>
                    {
                        ["seller_sku_list"] = $"[\"{externalId}\"]"
                    },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                if (result?.Code != "0")
                    return AdapterResult.Failure(
                        $"Failed to delete product: {result?.Message ?? content}",
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

                var offset = ((filter?.Page ?? 1) - 1) * (filter?.PageSize ?? 50);
                var limit = filter?.PageSize ?? 50;

                var parameters = new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(),
                    ["limit"] = limit.ToString(),
                    ["sort_by"] = "updated_at",
                    ["sort_direction"] = "DESC",
                };

                if (filter?.ModifiedAfter != null)
                    parameters["update_after"] = filter.ModifiedAfter.Value.ToString("yyyy-MM-dd HH:mm:ss");

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/orders/get",
                    parameters,
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaOrdersData>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {result?.Message ?? content}",
                        statusCode: (int)response.StatusCode);

                var orders = result.Data.Orders?.Select(MapToExternalOrder).ToList()
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

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/order/get",
                    new Dictionary<string, string> { ["order_id"] = externalId },
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<LazadaOrder>>(content, _json);

                if (result?.Code != "0" || result.Data == null)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

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

                // Lazada order status تتغير عبر SetStatusToReadyToShip أو SetStatusToShipped
                var apiPath = MapToLazadaStatusEndpoint(newStatus);
                if (string.IsNullOrEmpty(apiPath))
                    return AdapterResult.Failure($"Status '{newStatus}' not directly settable via API");

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    apiPath,
                    new Dictionary<string, string> { ["order_item_ids"] = $"[{externalId}]" },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                if (result?.Code != "0")
                    return AdapterResult.Failure(
                        $"Failed to update order status: {result?.Message ?? content}",
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
            // نجيب المنتجات وناخد الـ inventory منها
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
                    : new[] { new ExternalInventory
                    {
                        ExternalProductId = p.ExternalId,
                        Sku = p.Sku,
                        Quantity = p.StockQuantity
                    }})
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

                // Lazada: بيبعت XML payload للـ stock update
                var xmlPayload = BuildInventoryXml(items);

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/product/price_quantity/update",
                    new Dictionary<string, string> { ["payload"] = xmlPayload },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                if (result?.Code != "0")
                    return AdapterResult.Failure(
                        $"Failed to update inventory: {result?.Message ?? content}",
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

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/push/register",
                    new Dictionary<string, string>
                    {
                        ["push_url"] = $"{integration.StoreUrl}/webhooks/lazada"
                    },
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                return result?.Code == "0"
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to register webhooks: {result?.Message ?? content}",
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

                var url = BuildSignedUrl(
                    GetBaseUrl(integration),
                    "/push/unregister",
                    new Dictionary<string, string>(),
                    integration);

                var response = await _httpClient.PostAsync(url, null, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                var result = JsonSerializer.Deserialize<LazadaResponse<JsonElement>>(content, _json);

                return result?.Code == "0"
                    ? AdapterResult.Success()
                    : AdapterResult.Failure(
                        $"Failed to unregister webhooks: {result?.Message ?? content}",
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
            if (string.IsNullOrEmpty(_appSecret))
                return false;

            // Lazada webhook signature = HMAC-SHA256(AppSecret, payload)
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToUpper();

            return expected == signature.ToUpper();
        }

        // ── XML Builders ──────────────────────────────────────────────────────

        private static string BuildProductXml(ExternalProduct product, bool isUpdate = false)
        {
            var itemIdAttr = isUpdate ? $" ItemId=\"{product.ExternalId}\"" : string.Empty;

            var skusXml = product.Variants?.Count > 0
                ? string.Concat(product.Variants.Select(v => $@"
          <Sku>
            <SellerSku>{v.Sku}</SellerSku>
            <price>{v.Price}</price>
            <quantity>{v.StockQuantity}</quantity>
            <Status>active</Status>
          </Sku>"))
                : $@"
          <Sku>
            <SellerSku>{product.Sku}</SellerSku>
            <price>{product.Price}</price>
            <quantity>{product.StockQuantity}</quantity>
            <Status>{(product.IsActive ? "active" : "inactive")}</Status>
          </Sku>";

            return $@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<Request>
  <Product{itemIdAttr}>
    <PrimaryCategory>1</PrimaryCategory>
    <Attributes>
      <name>{product.Name}</name>
      <short_description>{product.Description ?? string.Empty}</short_description>
      <brand>No Brand</brand>
    </Attributes>
    <Skus>{skusXml}
    </Skus>
  </Product>
</Request>";
        }

        private static string BuildInventoryXml(IReadOnlyList<ExternalInventory> items)
        {
            var skusXml = string.Concat(items.Select(i => $@"
      <Sku>
        <ItemId>{i.ExternalProductId}</ItemId>
        <SellerSku>{i.Sku}</SellerSku>
        <Quantity>{i.Quantity}</Quantity>
      </Sku>"));

            return $@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<Request>
  <Product>
    <Skus>{skusXml}
    </Skus>
  </Product>
</Request>";
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(LazadaProductItem p) => new()
        {
            ExternalId = p.ItemId.ToString(),
            Name = p.Attributes?.Name ?? string.Empty,
            Description = p.Attributes?.ShortDescription,
            Sku = p.Skus?.FirstOrDefault()?.SellerSku,
            Price = p.Skus?.FirstOrDefault()?.Price ?? 0,
            StockQuantity = p.Skus?.Sum(s => s.Quantity) ?? 0,
            IsActive = p.Status == "active",
            ImageUrl = p.Images?.FirstOrDefault(),
            Categories = p.PrimaryCategory != 0 ? [p.PrimaryCategory.ToString()] : [],
            Variants = p.Skus?.Select(s => new ExternalProductVariant
            {
                ExternalId = s.SkuId.ToString(),
                Sku = s.SellerSku,
                Price = s.Price,
                StockQuantity = s.Quantity,
                Options = s.SkuData?.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value?.ToString() ?? string.Empty)
                    ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.UpdatedTime
        };

        private static ExternalOrder MapToExternalOrder(LazadaOrder o) => new()
        {
            ExternalId = o.OrderId.ToString(),
            OrderNumber = o.OrderNumber ?? o.OrderId.ToString(),
            Status = MapFromLazadaOrderStatus(o.Status),
            TotalAmount = o.Price,
            Currency = o.Currency ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                Name = o.AddressShipping?.FirstName + " " + o.AddressShipping?.LastName,
                Email = o.CustomerEmail,
                Phone = o.AddressShipping?.Phone
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ItemId.ToString(),
                ProductName = i.Name ?? string.Empty,
                Sku = i.Sku,
                Quantity = i.Units,
                UnitPrice = i.PaidPrice,
                TotalPrice = i.PaidPrice * i.Units
            }).ToList() ?? [],
            ShippingAddress = o.AddressShipping == null ? null : new ExternalAddress
            {
                Street = o.AddressShipping.Address,
                City = o.AddressShipping.City,
                Country = o.AddressShipping.Country,
                PostalCode = o.AddressShipping.PostCode
            },
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        };

        private static string MapFromLazadaOrderStatus(string? status) =>
            status?.ToLower() switch
            {
                "unpaid" => "pending",
                "pending" => "pending",
                "ready_to_ship" => "processing",
                "shipped" => "shipped",
                "delivered" => "delivered",
                "canceled" => "cancelled",
                "returned" => "returned",
                "failed" => "cancelled",
                _ => "pending"
            };

        private static string? MapToLazadaStatusEndpoint(string localStatus) =>
            localStatus.ToLower() switch
            {
                "processing" => "/order/rts",           // Ready to Ship
                "shipped" => "/order/shipped",
                "cancelled" => "/order/cancel",
                _ => null
            };
    }

    // ── Lazada API Models ──────────────────────────────────────────────────────

    internal class LazadaResponse<T>
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? RequestId { get; set; }
        public T? Data { get; set; }
    }

    internal class LazadaTokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public long ExpiresIn { get; set; }
        public long RefreshExpiresIn { get; set; }
        public string? Country { get; set; }
    }

    internal class LazadaProductsData
    {
        public List<LazadaProductItem>? Products { get; set; }
        public int TotalProducts { get; set; }
    }

    internal class LazadaProductItem
    {
        public long ItemId { get; set; }
        public string? Status { get; set; }
        public int PrimaryCategory { get; set; }
        public List<string>? Images { get; set; }
        public LazadaProductAttributes? Attributes { get; set; }
        public List<LazadaSku>? Skus { get; set; }
        public DateTime? UpdatedTime { get; set; }
    }

    internal class LazadaProductAttributes
    {
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? Brand { get; set; }
    }

    internal class LazadaSku
    {
        public long SkuId { get; set; }
        public string? SellerSku { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public Dictionary<string, object?>? SkuData { get; set; }
    }

    internal class LazadaCreateProductData
    {
        public long? ItemId { get; set; }
    }

    internal class LazadaOrdersData
    {
        public List<LazadaOrder>? Orders { get; set; }
        public int Count { get; set; }
        public int CountTotal { get; set; }
    }

    internal class LazadaOrder
    {
        public long OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public decimal Price { get; set; }
        public string? Currency { get; set; }
        public string? CustomerEmail { get; set; }
        public LazadaAddress? AddressShipping { get; set; }
        public List<LazadaOrderItem>? Items { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    internal class LazadaAddress
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostCode { get; set; }
    }

    internal class LazadaOrderItem
    {
        public long ItemId { get; set; }
        public string? Name { get; set; }
        public string? Sku { get; set; }
        public int Units { get; set; }
        public decimal PaidPrice { get; set; }
    }
}