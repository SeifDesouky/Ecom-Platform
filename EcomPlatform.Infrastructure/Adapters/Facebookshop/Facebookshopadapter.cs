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

namespace EcomPlatform.Infrastructure.Adapters.FacebookShop
{
    /// <summary>
    /// Facebook Shop Adapter — Meta Commerce Platform
    /// Auth: OAuth2 Meta (نفس Instagram Shop)
    /// Docs: https://developers.facebook.com/docs/commerce-platform
    /// StoreIntegration:
    ///   ApiKey          = Page Access Token
    ///   RefreshToken    = Long-lived User Access Token
    ///   ExternalStoreId = Catalog ID
    ///   WebhookSecret   = App Secret (للـ webhook verification)
    /// appsettings:
    ///   FacebookShop:AppId     = Meta App ID
    ///   FacebookShop:AppSecret = Meta App Secret
    /// </summary>
    public class FacebookShopAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://graph.facebook.com/v19.0";

        private readonly HttpClient _httpClient;
        private readonly string _appId;
        private readonly string _appSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.FacebookShop;

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

        public FacebookShopAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _appId = configuration["FacebookShop:AppId"] ?? string.Empty;
            _appSecret = configuration["FacebookShop:AppSecret"] ?? string.Empty;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetAuthHeaders(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Meta: كل request بيحتاج access_token كـ query param
        /// </summary>
        private string WithToken(string url, StoreIntegration integration)
        {
            var separator = url.Contains('?') ? "&" : "?";
            return $"{url}{separator}access_token={Uri.EscapeDataString(integration.ApiKey ?? string.Empty)}";
        }

        /// <summary>
        /// App Secret Proof = HMAC-SHA256(AppSecret, AccessToken)
        /// مطلوب لـ server-side calls
        /// </summary>
        private string BuildAppSecretProof(string accessToken)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(accessToken));
            return Convert.ToHexString(hash).ToLower();
        }

        private string WithTokenAndProof(string url, StoreIntegration integration)
        {
            var token = integration.ApiKey ?? string.Empty;
            var proof = BuildAppSecretProof(token);
            var separator = url.Contains('?') ? "&" : "?";
            return $"{url}{separator}access_token={Uri.EscapeDataString(token)}&appsecret_proof={proof}";
        }

        // ── Connection ────────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                var url = WithTokenAndProof($"{BaseUrl}/me", integration);
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);

                if (error?.Error?.Code == 190)
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure(
                    $"Connection failed: {error?.Error?.Message ?? content}",
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

                // Meta: exchange short-lived → long-lived token
                // Long-lived tokens تدوم ~60 يوم ومش بيتعمل refresh تقليدي
                // بنعمل extend لأقصى مدة
                var url = $"{BaseUrl}/oauth/access_token" +
                          $"?grant_type=fb_exchange_token" +
                          $"&client_id={_appId}" +
                          $"&client_secret={_appSecret}" +
                          $"&fb_exchange_token={Uri.EscapeDataString(integration.RefreshToken ?? string.Empty)}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult<TokenData>.Failure(
                        $"Token refresh failed: {error?.Error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

                var token = JsonSerializer.Deserialize<FacebookTokenResponse>(content, _json);
                if (token == null || string.IsNullOrEmpty(token.AccessToken))
                    return AdapterResult<TokenData>.Failure("Failed to parse token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.AccessToken, // Meta: نفس الـ token يُستخدم للـ refresh
                    ExpiresAt = token.ExpiresIn > 0
                        ? DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                        : DateTime.UtcNow.AddDays(60)
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

                var catalogId = integration.ExternalStoreId;
                var limit = filter?.PageSize ?? 50;
                var fields = "id,name,description,retailer_id,price,sale_price,availability,condition,image_url,url,category,inventory,additional_variant_attributes,custom_data";

                var url = WithTokenAndProof(
                    $"{BaseUrl}/{catalogId}/products?fields={fields}&limit={limit}",
                    integration);

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_time={DateTimeOffset.Parse(filter.ModifiedAfter.Value.ToString()).ToUnixTimeSeconds()}";

                var products = new List<ExternalProduct>();
                string? nextPage = url;

                // Facebook بيستخدم cursor-based pagination
                while (nextPage != null)
                {
                    var response = await _httpClient.GetAsync(nextPage, ct);
                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var result = JsonSerializer.Deserialize<FacebookPagedResponse<FacebookProduct>>(content, _json);

                    if (result?.Data != null)
                        products.AddRange(result.Data.Select(MapToExternalProduct));

                    // بعد الـ page الأولى نوقف — مش هنعمل full pagination في call واحدة
                    nextPage = null;
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

                var fields = "id,name,description,retailer_id,price,sale_price,availability,condition,image_url,url,category,inventory";
                var url = WithTokenAndProof(
                    $"{BaseUrl}/{externalId}?fields={fields}",
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var product = JsonSerializer.Deserialize<FacebookProduct>(content, _json);
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

                var catalogId = integration.ExternalStoreId;
                var url = WithTokenAndProof(
                    $"{BaseUrl}/{catalogId}/products",
                    integration);

                // Facebook Commerce: price بيتبعت كـ string "1999 EGP"
                var priceString = $"{(int)(product.Price * 100)} EGP";

                var body = new
                {
                    retailer_id = product.Sku ?? product.ExternalId,
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    price = priceString,
                    currency = "EGP",
                    availability = product.IsActive
                        ? (product.StockQuantity > 0 ? "in stock" : "out of stock")
                        : "discontinued",
                    condition = "new",
                    image_url = product.ImageUrl ?? string.Empty,
                    url = $"https://placeholder.com/products/{product.Sku}",
                    inventory = product.StockQuantity
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {error?.Error?.Message ?? content}",
                        statusCode: (int)response.StatusCode);
                }

                var result = JsonSerializer.Deserialize<FacebookCreateResponse>(content, _json);
                if (string.IsNullOrEmpty(result?.Id))
                    return AdapterResult<string>.Failure("Product created but ID not returned");

                return AdapterResult<string>.Success(result.Id);
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

                var url = WithTokenAndProof(
                    $"{BaseUrl}/{product.ExternalId}",
                    integration);

                var priceString = $"{(int)(product.Price * 100)} EGP";

                var body = new
                {
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    price = priceString,
                    availability = product.IsActive
                        ? (product.StockQuantity > 0 ? "in stock" : "out of stock")
                        : "discontinued",
                    inventory = product.StockQuantity,
                    image_url = product.ImageUrl ?? string.Empty
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to update product: {error?.Error?.Message ?? content}",
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

                var url = WithTokenAndProof($"{BaseUrl}/{externalId}", integration);
                var response = await _httpClient.DeleteAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to delete product: {error?.Error?.Message ?? content}",
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

                // Facebook Commerce Orders — عبر Page ID
                var pageId = integration.ExternalStoreId;
                var limit = filter?.PageSize ?? 50;
                var fields = "id,order_status,created,last_updated,ship_by_date,merchant_order_id,channel,selected_shipping_option,shipping_address,estimated_payment_details,buyer_details,items{id,retailer_id,product_id,name,quantity,price_per_unit,tax_details}";

                var url = WithTokenAndProof(
                    $"{BaseUrl}/{pageId}/commerce_orders?fields={fields}&limit={limit}",
                    integration);

                if (filter?.ModifiedAfter != null)
                    url += $"&updated_before={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" +
                           $"&updated_after={DateTimeOffset.Parse(filter.ModifiedAfter.Value.ToString()).ToUnixTimeSeconds()}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var result = JsonSerializer.Deserialize<FacebookPagedResponse<FacebookOrder>>(content, _json);
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

                var fields = "id,order_status,created,last_updated,merchant_order_id,channel,selected_shipping_option,shipping_address,estimated_payment_details,buyer_details,items{id,retailer_id,product_id,name,quantity,price_per_unit}";
                var url = WithTokenAndProof(
                    $"{BaseUrl}/{externalId}?fields={fields}",
                    integration);

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var order = JsonSerializer.Deserialize<FacebookOrder>(content, _json);
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

                var fbStatus = MapToFacebookOrderStatus(newStatus);
                if (fbStatus == null)
                    return AdapterResult.Failure(
                        $"Status '{newStatus}' not supported by Facebook Commerce API");

                var url = WithTokenAndProof(
                    $"{BaseUrl}/{externalId}",
                    integration);

                object body = fbStatus == "FULFILLED"
                    ? new
                    {
                        state = fbStatus,
                        tracking_info = new
                        {
                            tracking_number = "N/A",
                            carrier = "OTHER",
                            shipping_method_name = "Standard Shipping"
                        }
                    }
                    : new { state = fbStatus };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to update order status: {error?.Error?.Message ?? content}",
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

                // Facebook: batch update عبر /products endpoint لكل item
                var errors = new List<string>();

                foreach (var item in items)
                {
                    var url = WithTokenAndProof(
                        $"{BaseUrl}/{item.ExternalProductId}",
                        integration);

                    var body = new { inventory = item.Quantity };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync(url, request, ct);
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

        // ── Webhooks ──────────────────────────────────────────────────────────

        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeaders(integration);

                // Meta Webhooks بتتسجل على مستوى الـ App عبر App Dashboard أو Graph API
                var url = WithTokenAndProof(
                    $"{BaseUrl}/{_appId}/subscriptions",
                    integration);

                var body = new
                {
                    @object = "page",
                    callback_url = $"{integration.StoreUrl}/webhooks/facebook",
                    verify_token = integration.WebhookSecret ?? Guid.NewGuid().ToString(),
                    fields = string.Join(",", new[]
                    {
                        "commerce_order",
                        "commerce_merchant_settings_updated",
                        "product_item"
                    })
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to register webhooks: {error?.Error?.Message ?? content}",
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

                var url = WithTokenAndProof(
                    $"{BaseUrl}/{_appId}/subscriptions?object=page",
                    integration);

                var response = await _httpClient.DeleteAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = JsonSerializer.Deserialize<FacebookErrorResponse>(content, _json);
                    return AdapterResult.Failure(
                        $"Failed to unregister webhooks: {error?.Error?.Message ?? content}",
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
            // Meta: X-Hub-Signature-256: sha256=HASH
            // بنستخدم AppSecret مش WebhookSecret
            var secret = _appSecret;
            if (string.IsNullOrEmpty(secret)) return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = $"sha256={Convert.ToHexString(hash).ToLower()}";

            // Remove "sha256=" prefix من الـ signature لو موجود
            var normalizedSignature = signature.StartsWith("sha256=")
                ? signature
                : $"sha256={signature}";

            return expected == normalizedSignature.ToLower();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(FacebookProduct p) => new()
        {
            ExternalId = p.Id ?? string.Empty,
            Name = p.Name ?? string.Empty,
            Description = p.Description,
            Sku = p.RetailerId,
            // Facebook price: "1999 EGP" → نحولها لـ decimal
            Price = ParseFacebookPrice(p.Price),
            StockQuantity = p.Inventory,
            IsActive = p.Availability == "in stock" || p.Availability == "available for order",
            ImageUrl = p.ImageUrl,
            Categories = p.Category != null ? [p.Category] : [],
            Variants = [],
            UpdatedAt = null
        };

        private static ExternalOrder MapToExternalOrder(FacebookOrder o) => new()
        {
            ExternalId = o.Id ?? string.Empty,
            OrderNumber = o.MerchantOrderId ?? o.Id ?? string.Empty,
            Status = MapFromFacebookOrderStatus(o.OrderStatus?.State),
            TotalAmount = o.EstimatedPaymentDetails?.TotalAmount?.Amount ?? 0,
            Currency = o.EstimatedPaymentDetails?.TotalAmount?.Currency ?? "USD",
            Customer = o.BuyerDetails == null ? null : new ExternalCustomerInfo
            {
                Name = o.BuyerDetails.Name,
                Email = o.BuyerDetails.Email
            },
            Items = o.Items?.Data?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.ProductId ?? string.Empty,
                ProductName = i.Name ?? string.Empty,
                Sku = i.RetailerId,
                Quantity = i.Quantity,
                UnitPrice = i.PricePerUnit?.Amount ?? 0,
                TotalPrice = (i.PricePerUnit?.Amount ?? 0) * i.Quantity
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddress == null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.Street1,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.Country,
                PostalCode = o.ShippingAddress.PostalCode
            },
            CreatedAt = o.Created ?? DateTime.UtcNow,
            UpdatedAt = o.LastUpdated
        };

        private static decimal ParseFacebookPrice(string? price)
        {
            if (string.IsNullOrEmpty(price)) return 0;
            // Format: "1999 EGP" أو "19.99 USD"
            var parts = price.Split(' ');
            return decimal.TryParse(parts[0], out var val) ? val : 0;
        }

        private static string MapFromFacebookOrderStatus(string? state) =>
            state?.ToUpper() switch
            {
                "CREATED" => "pending",
                "PROCESSING" => "processing",
                "FULFILLED" => "shipped",
                "COMPLETED" => "delivered",
                "CANCELLED" => "cancelled",
                "REFUNDED" => "returned",
                "IN_PROGRESS" => "processing",
                _ => "pending"
            };

        private static string? MapToFacebookOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "processing" => "IN_PROGRESS",
                "shipped" => "FULFILLED",
                "delivered" => "COMPLETED",
                "cancelled" => "CANCELLED",
                _ => null
            };
    }

    // ── Facebook API Models ────────────────────────────────────────────────────

    internal class FacebookPagedResponse<T>
    {
        public List<T>? Data { get; set; }
        public FacebookPaging? Paging { get; set; }
    }

    internal class FacebookPaging
    {
        public FacebookCursors? Cursors { get; set; }
        public string? Next { get; set; }
        public string? Previous { get; set; }
    }

    internal class FacebookCursors
    {
        public string? Before { get; set; }
        public string? After { get; set; }
    }

    internal class FacebookTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    internal class FacebookErrorResponse
    {
        public FacebookError? Error { get; set; }
    }

    internal class FacebookError
    {
        public string? Message { get; set; }
        public string? Type { get; set; }
        public int Code { get; set; }
        public int ErrorSubcode { get; set; }
    }

    internal class FacebookCreateResponse
    {
        public string? Id { get; set; }
    }

    internal class FacebookProduct
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? RetailerId { get; set; }
        public string? Price { get; set; }       // "1999 EGP"
        public string? SalePrice { get; set; }
        public string? Availability { get; set; } // "in stock" | "out of stock" | "discontinued"
        public string? Condition { get; set; }
        public string? ImageUrl { get; set; }
        public string? Url { get; set; }
        public string? Category { get; set; }
        public int Inventory { get; set; }
    }

    internal class FacebookOrder
    {
        public string? Id { get; set; }
        public FacebookOrderStatus? OrderStatus { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string? MerchantOrderId { get; set; }
        public string? Channel { get; set; }
        public FacebookShippingAddress? ShippingAddress { get; set; }
        public FacebookPaymentDetails? EstimatedPaymentDetails { get; set; }
        public FacebookBuyerDetails? BuyerDetails { get; set; }
        public FacebookOrderItemsWrapper? Items { get; set; }
    }

    internal class FacebookOrderStatus
    {
        public string? State { get; set; }
    }

    internal class FacebookShippingAddress
    {
        public string? Name { get; set; }
        public string? Street1 { get; set; }
        public string? Street2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }

    internal class FacebookPaymentDetails
    {
        public FacebookMoney? TotalAmount { get; set; }
        public FacebookMoney? Tax { get; set; }
        public FacebookMoney? Subtotal { get; set; }
    }

    internal class FacebookMoney
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
    }

    internal class FacebookBuyerDetails
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    internal class FacebookOrderItemsWrapper
    {
        public List<FacebookOrderItem>? Data { get; set; }
    }

    internal class FacebookOrderItem
    {
        public string? Id { get; set; }
        public string? RetailerId { get; set; }
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public FacebookMoney? PricePerUnit { get; set; }
    }
}