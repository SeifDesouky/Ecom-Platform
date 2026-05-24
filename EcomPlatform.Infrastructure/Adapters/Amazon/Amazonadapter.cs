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

namespace EcomPlatform.Infrastructure.Adapters.Amazon
{
    /// <summary>
    /// Amazon SP-API Adapter
    /// Auth: Login With Amazon (LWA) — OAuth2 Client Credentials
    /// Docs: https://developer-docs.amazon.com/sp-api/docs
    /// </summary>
    public class AmazonAdapter : IMarketplaceAdapter
    {
        private const string SpApiBaseUrl = "https://sellingpartnerapi-na.amazon.com";
        private const string LwaTokenUrl = "https://api.amazon.com/auth/o2/token";
        private const string AwsRegion = "us-east-1";
        private const string AwsService = "execute-api";

        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _awsAccessKey;
        private readonly string _awsSecretKey;
        private readonly string _roleArn;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Amazon;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = true,
            SupportsOrders = true,
            SupportsCustomers = false,  // SP-API لا يعطي PII بدون إذن خاص
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

        public AmazonAdapter(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["Amazon:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Amazon:ClientSecret"] ?? string.Empty;
            _awsAccessKey = configuration["Amazon:AwsAccessKey"] ?? string.Empty;
            _awsSecretKey = configuration["Amazon:AwsSecretKey"] ?? string.Empty;
            _roleArn = configuration["Amazon:RoleArn"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER"; // US marketplace default
                var path = $"/sellers/v1/marketplaceParticipations";

                var response = await SendSignedRequestAsync(integration, HttpMethod.Get, path, ct: ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid or expired LWA token", "UNAUTHORIZED", 401);

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
                // LWA — Login With Amazon token refresh
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = integration.RefreshToken ?? string.Empty,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var response = await _httpClient.PostAsync(LwaTokenUrl, body, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<TokenData>.Failure(
                        $"LWA token refresh failed: {content}",
                        statusCode: (int)response.StatusCode);

                var token = JsonSerializer.Deserialize<AmazonTokenResponse>(content, _json);
                if (token is null)
                    return AdapterResult<TokenData>.Failure("Failed to parse LWA token response");

                return AdapterResult<TokenData>.Success(new TokenData
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken ?? integration.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                });
            }
            catch (Exception ex)
            {
                return AdapterResult<TokenData>.Failure($"Error: {ex.Message}");
            }
        }

        // ── Products (Catalog Items API v2022-04-01) ──────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var pageToken = string.Empty;
                var allProducts = new List<ExternalProduct>();
                var pageSize = filter?.PageSize ?? 20; // SP-API max = 20 per page

                do
                {
                    var query = $"?marketplaceIds={marketplaceId}&includedData=summaries,attributes,dimensions,identifiers,images,productTypes,salesRanks&pageSize={pageSize}";
                    if (!string.IsNullOrEmpty(pageToken))
                        query += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Get,
                        $"/catalog/2022-04-01/items{query}", ct: ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                            $"Failed to get products: {content}",
                            statusCode: (int)response.StatusCode);

                    var catalogResponse = JsonSerializer.Deserialize<AmazonCatalogResponse>(content, _json);
                    if (catalogResponse?.Items is not null)
                        allProducts.AddRange(catalogResponse.Items.Select(MapToExternalProduct));

                    pageToken = catalogResponse?.Pagination?.NextToken ?? string.Empty;

                    // إذا في filter?.Page محدد، نرجع صفحة واحدة بس
                    if (filter != null && filter.Page > 0) break;

                } while (!string.IsNullOrEmpty(pageToken));

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
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var path = $"/catalog/2022-04-01/items/{externalId}?marketplaceIds={marketplaceId}&includedData=summaries,attributes,identifiers,images";

                var response = await SendSignedRequestAsync(integration, HttpMethod.Get, path, ct: ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var item = JsonSerializer.Deserialize<AmazonCatalogItem>(content, _json);
                if (item is null)
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
                // Amazon SP-API: إنشاء listing عبر Listings Items API
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var sellerId = integration.ExternalStoreId ?? string.Empty;
                var sku = product.Sku ?? Guid.NewGuid().ToString("N")[..12].ToUpper();
                var path = $"/listings/2021-08-01/items/{sellerId}/{Uri.EscapeDataString(sku)}";

                var body = new
                {
                    productType = "PRODUCT",
                    attributes = new
                    {
                        item_name = new[] { new { value = product.Name, marketplace_id = marketplaceId } },
                        item_description = product.Description is not null
                            ? new[] { new { value = product.Description, marketplace_id = marketplaceId } }
                            : null,
                        list_price = new[] { new
                        {
                            value            = product.Price,
                            currency         = "USD",
                            marketplace_id   = marketplaceId
                        }},
                        fulfillment_availability = new[] { new
                        {
                            fulfillment_channel_code = "DEFAULT",
                            quantity                 = product.StockQuantity,
                            marketplace_id           = marketplaceId
                        }}
                    }
                };

                var response = await SendSignedRequestAsync(
                    integration, HttpMethod.Put, path,
                    JsonSerializer.Serialize(body, _json), ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create listing: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult<string>.Success(sku);
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
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var sellerId = integration.ExternalStoreId ?? string.Empty;
                var sku = product.Sku ?? product.ExternalId;
                var path = $"/listings/2021-08-01/items/{sellerId}/{Uri.EscapeDataString(sku)}";

                var body = new
                {
                    productType = "PRODUCT",
                    patches = new[]
                    {
                        new
                        {
                            op    = "replace",
                            path  = "/attributes",
                            value = new
                            {
                                item_name = new[] { new { value = product.Name, marketplace_id = marketplaceId } },
                                list_price = new[] { new
                                {
                                    value          = product.Price,
                                    currency       = "USD",
                                    marketplace_id = marketplaceId
                                }}
                            }
                        }
                    }
                };

                var response = await SendSignedRequestAsync(
                    integration, HttpMethod.Put, path,
                    JsonSerializer.Serialize(body, _json), ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to update listing: {content}",
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
                var sellerId = integration.ExternalStoreId ?? string.Empty;
                var path = $"/listings/2021-08-01/items/{sellerId}/{Uri.EscapeDataString(externalId)}";

                var response = await SendSignedRequestAsync(
                    integration, HttpMethod.Delete, path, ct: ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult.Failure(
                        $"Failed to delete listing: {content}",
                        statusCode: (int)response.StatusCode);

                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Orders (Orders API v0) ────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var createdAfter = filter?.ModifiedAfter ?? DateTime.UtcNow.AddDays(-7);
                var allOrders = new List<ExternalOrder>();
                var nextToken = string.Empty;

                do
                {
                    string query;
                    if (!string.IsNullOrEmpty(nextToken))
                        query = $"?NextToken={Uri.EscapeDataString(nextToken)}";
                    else
                        query = $"?MarketplaceIds={marketplaceId}&CreatedAfter={createdAfter:yyyy-MM-ddTHH:mm:ssZ}";

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Get, $"/orders/v0/orders{query}", ct: ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                            $"Failed to get orders: {content}",
                            statusCode: (int)response.StatusCode);

                    var ordersResponse = JsonSerializer.Deserialize<AmazonOrdersResponse>(content, _json);

                    if (ordersResponse?.Payload?.Orders is not null)
                    {
                        foreach (var order in ordersResponse.Payload.Orders)
                        {
                            // جلب order items بشكل منفصل (Amazon SP-API requirement)
                            var itemsResult = await GetOrderItemsAsync(integration, order.AmazonOrderId, ct);
                            order.Items = itemsResult;
                            allOrders.Add(MapToExternalOrder(order));
                        }
                    }

                    nextToken = ordersResponse?.Payload?.NextToken ?? string.Empty;

                    if (filter != null && filter.Page > 0) break;

                } while (!string.IsNullOrEmpty(nextToken));

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
                var response = await SendSignedRequestAsync(
                    integration, HttpMethod.Get, $"/orders/v0/orders/{externalId}", ct: ct);

                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var orderResponse = JsonSerializer.Deserialize<AmazonSingleOrderResponse>(content, _json);
                if (orderResponse?.Payload is null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                var items = await GetOrderItemsAsync(integration, externalId, ct);
                orderResponse.Payload.Items = items;

                return AdapterResult<ExternalOrder>.Success(MapToExternalOrder(orderResponse.Payload));
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
            // Amazon SP-API لا يدعم تعديل status الأوردر مباشرة من الـ seller
            // يتم عبر Shipment Confirmation API أو MFN Shipment
            try
            {
                if (newStatus.ToLower() == "shipped")
                {
                    var path = $"/orders/v0/orders/{externalId}/shipment";
                    var body = new
                    {
                        marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER",
                        shipFromAddress = new { name = "Seller", addressLine1 = "N/A", city = "N/A", countryCode = "US" },
                        orderItems = Array.Empty<object>()
                    };

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Post, path,
                        JsonSerializer.Serialize(body, _json), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        return AdapterResult.Failure(
                            $"Failed to confirm shipment: {content}",
                            statusCode: (int)response.StatusCode);
                    }
                }

                // بقية الـ statuses غير مدعومة مباشرة
                return AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Inventory (FBA Inventory API) ─────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                var marketplaceId = integration.ExternalStoreId ?? "ATVPDKIKX0DER";
                var allInventory = new List<ExternalInventory>();
                var nextToken = string.Empty;

                do
                {
                    var query = string.IsNullOrEmpty(nextToken)
                        ? $"?details=true&marketplaceIds={marketplaceId}&granularityType=Marketplace&granularityId={marketplaceId}"
                        : $"?details=true&marketplaceIds={marketplaceId}&granularityType=Marketplace&granularityId={marketplaceId}&nextToken={Uri.EscapeDataString(nextToken)}";

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Get,
                        $"/fba/inventory/v1/summaries{query}", ct: ct);

                    var content = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        return AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(
                            $"Failed to get inventory: {content}",
                            statusCode: (int)response.StatusCode);

                    var invResponse = JsonSerializer.Deserialize<AmazonInventoryResponse>(content, _json);

                    if (invResponse?.Payload?.InventorySummaries is not null)
                    {
                        allInventory.AddRange(invResponse.Payload.InventorySummaries.Select(i => new ExternalInventory
                        {
                            ExternalProductId = i.Asin ?? string.Empty,
                            Sku = i.SellerSku,
                            Quantity = i.TotalQuantity
                        }));
                    }

                    nextToken = invResponse?.Payload?.NextToken ?? string.Empty;

                } while (!string.IsNullOrEmpty(nextToken));

                return AdapterResult<IReadOnlyList<ExternalInventory>>.Success(allInventory);
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
                var sellerId = integration.ExternalStoreId ?? string.Empty;
                var errors = new List<string>();

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.Sku)) continue;

                    var path = $"/listings/2021-08-01/items/{sellerId}/{Uri.EscapeDataString(item.Sku)}";
                    var body = new
                    {
                        productType = "PRODUCT",
                        patches = new[]
                        {
                            new
                            {
                                op    = "replace",
                                path  = "/attributes/fulfillment_availability",
                                value = new[]
                                {
                                    new
                                    {
                                        fulfillment_channel_code = "DEFAULT",
                                        quantity                 = item.Quantity,
                                        marketplace_id           = integration.ExternalStoreId ?? "ATVPDKIKX0DER"
                                    }
                                }
                            }
                        }
                    };

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Put, path,
                        JsonSerializer.Serialize(body, _json), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"SKU {item.Sku}: {content}");
                    }
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Some inventory updates failed: {string.Join(" | ", errors)}")
                    : AdapterResult.Success();
            }
            catch (Exception ex)
            {
                return AdapterResult.Failure($"Error: {ex.Message}");
            }
        }

        // ── Webhooks (Amazon Notifications API) ───────────────────────────

        public async Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
        {
            try
            {
                // Amazon SP-API يستخدم SQS/SNS بدلاً من webhooks تقليدية
                // نسجل subscription لكل notification type
                var errors = new List<string>();

                foreach (var eventType in eventTypes)
                {
                    var amazonEventType = MapToAmazonEventType(eventType);
                    if (string.IsNullOrEmpty(amazonEventType)) continue;

                    var path = $"/notifications/v1/subscriptions/{amazonEventType}";
                    var body = new
                    {
                        payloadVersion = "1.0",
                        destinationId = integration.WebhookSecret ?? string.Empty
                    };

                    var response = await SendSignedRequestAsync(
                        integration, HttpMethod.Post, path,
                        JsonSerializer.Serialize(body, _json), ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"{amazonEventType}: {content}");
                    }
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Some subscriptions failed: {string.Join(" | ", errors)}")
                    : AdapterResult.Success();
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
                var eventTypes = new[] { "ORDER_CHANGE", "ITEM_INVENTORY_EVENT_CHANGE", "LISTINGS_ITEM_STATUS_CHANGE" };
                var errors = new List<string>();

                foreach (var eventType in eventTypes)
                {
                    // جلب subscription ID أولاً
                    var getResponse = await SendSignedRequestAsync(
                        integration, HttpMethod.Get,
                        $"/notifications/v1/subscriptions/{eventType}", ct: ct);

                    if (!getResponse.IsSuccessStatusCode) continue;

                    var getContent = await getResponse.Content.ReadAsStringAsync(ct);
                    var subscriptions = JsonSerializer.Deserialize<AmazonSubscriptionsResponse>(getContent, _json);
                    var subscriptionId = subscriptions?.Payload?.SubscriptionId;

                    if (string.IsNullOrEmpty(subscriptionId)) continue;

                    var deleteResponse = await SendSignedRequestAsync(
                        integration, HttpMethod.Delete,
                        $"/notifications/v1/subscriptions/{eventType}/{subscriptionId}", ct: ct);

                    if (!deleteResponse.IsSuccessStatusCode)
                        errors.Add(eventType);
                }

                return errors.Count > 0
                    ? AdapterResult.Failure($"Some unsubscriptions failed: {string.Join(", ", errors)}")
                    : AdapterResult.Success();
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
            // Amazon SNS signature verification
            // في الـ production يجب التحقق من SNS certificate
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(integration.WebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToBase64String(hash);

            return expected == signature;
        }

        // ── Private Helpers ───────────────────────────────────────────────

        /// <summary>
        /// إرسال HTTP request موقّع بـ AWS Signature Version 4 + LWA Token
        /// </summary>
        private async Task<HttpResponseMessage> SendSignedRequestAsync(
            StoreIntegration integration,
            HttpMethod method,
            string pathAndQuery,
            string? jsonBody = null,
            CancellationToken ct = default)
        {
            var uri = new Uri($"{SpApiBaseUrl}{pathAndQuery}");
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString("yyyyMMdd");
            var amzDate = now.ToString("yyyyMMddTHHmmssZ");

            var request = new HttpRequestMessage(method, uri);

            // LWA Access Token
            request.Headers.Add("x-amz-access-token", integration.ApiKey ?? string.Empty);
            request.Headers.Add("x-amz-date", amzDate);
            request.Headers.Host = uri.Host;

            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                request.Headers.Add("x-amz-content-sha256", ComputeSha256Hash(jsonBody));
            }
            else
            {
                request.Headers.Add("x-amz-content-sha256", ComputeSha256Hash(string.Empty));
            }

            // AWS SigV4 Signing (simplified — في production استخدم AWS SDK)
            var authHeader = BuildSigV4Header(
                method.Method, uri, amzDate, dateStamp,
                jsonBody ?? string.Empty);

            request.Headers.Authorization = new AuthenticationHeaderValue("AWS4-HMAC-SHA256", authHeader);

            return await _httpClient.SendAsync(request, ct);
        }

        /// <summary>
        /// AWS Signature Version 4 — Canonical Request Builder
        /// </summary>
        private string BuildSigV4Header(
            string httpMethod,
            Uri uri,
            string amzDate,
            string dateStamp,
            string payload)
        {
            var canonicalUri = uri.AbsolutePath;
            var canonicalQuery = string.Join("&",
                uri.Query.TrimStart('?').Split('&')
                   .OrderBy(x => x));

            var payloadHash = ComputeSha256Hash(payload);
            var canonicalHeaders = $"host:{uri.Host}\nx-amz-date:{amzDate}\n";
            var signedHeaders = "host;x-amz-date";

            var canonicalRequest = string.Join("\n",
                httpMethod, canonicalUri, canonicalQuery,
                canonicalHeaders, signedHeaders, payloadHash);

            var credentialScope = $"{dateStamp}/{AwsRegion}/{AwsService}/aws4_request";
            var stringToSign = string.Join("\n",
                "AWS4-HMAC-SHA256", amzDate,
                credentialScope, ComputeSha256Hash(canonicalRequest));

            var signingKey = GetSigV4SigningKey(dateStamp);
            var signature = ToHexString(ComputeHmac(signingKey, stringToSign));

            return $"Credential={_awsAccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        }

        private byte[] GetSigV4SigningKey(string dateStamp) =>
            ComputeHmac(
                ComputeHmac(
                    ComputeHmac(
                        ComputeHmac(
                            Encoding.UTF8.GetBytes($"AWS4{_awsSecretKey}"),
                            dateStamp),
                        AwsRegion),
                    AwsService),
                "aws4_request");

        private static byte[] ComputeHmac(byte[] key, string data) =>
            new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

        private static string ComputeSha256Hash(string data)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
            return ToHexString(bytes);
        }

        private static string ToHexString(byte[] bytes) =>
            Convert.ToHexString(bytes).ToLower();

        private async Task<List<AmazonOrderItem>> GetOrderItemsAsync(
            StoreIntegration integration,
            string orderId,
            CancellationToken ct)
        {
            try
            {
                var response = await SendSignedRequestAsync(
                    integration, HttpMethod.Get,
                    $"/orders/v0/orders/{orderId}/orderItems", ct: ct);

                if (!response.IsSuccessStatusCode) return [];

                var content = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<AmazonOrderItemsResponse>(content, _json);
                return result?.Payload?.OrderItems ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static string MapToAmazonEventType(string localEventType) =>
            localEventType.ToLower() switch
            {
                "order.created" => "ORDER_CHANGE",
                "order.updated" => "ORDER_CHANGE",
                "inventory.update" => "ITEM_INVENTORY_EVENT_CHANGE",
                "product.updated" => "LISTINGS_ITEM_STATUS_CHANGE",
                _ => string.Empty
            };

        // ── Mapping ───────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(AmazonCatalogItem item) => new()
        {
            ExternalId = item.Asin ?? string.Empty,
            Name = item.Summaries?.FirstOrDefault()?.ItemName ?? string.Empty,
            Description = item.Attributes?.GetValueOrDefault("product_description")?.ToString(),
            Sku = item.Identifiers?.FirstOrDefault()?.Identifiers
                                ?.FirstOrDefault(i => i.IdentifierType == "SKU")?.Identifier,
            Price = 0, // السعر يجيء من Pricing API منفصل
            StockQuantity = 0, // الـ inventory يجيء من FBA Inventory API
            IsActive = item.Summaries?.FirstOrDefault()?.Status == "BUYABLE",
            ImageUrl = item.Images?.FirstOrDefault()?.Images?.FirstOrDefault()?.Link,
            Categories = item.SalesRanks?.Select(r => r.Title ?? string.Empty).ToList() ?? [],
            Variants = []
        };

        private static ExternalOrder MapToExternalOrder(AmazonOrder o) => new()
        {
            ExternalId = o.AmazonOrderId,
            OrderNumber = o.SellerOrderId ?? o.AmazonOrderId,
            Status = MapFromAmazonOrderStatus(o.OrderStatus ?? string.Empty),
            TotalAmount = decimal.TryParse(o.OrderTotal?.Amount, out var total) ? total : 0,
            Currency = o.OrderTotal?.CurrencyCode ?? "USD",
            Customer = new ExternalCustomerInfo
            {
                ExternalId = o.BuyerInfo?.BuyerEmail ?? string.Empty,
                Name = o.BuyerInfo?.BuyerName ?? string.Empty,
                Email = o.BuyerInfo?.BuyerEmail ?? string.Empty
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.Asin ?? string.Empty,
                ProductName = i.Title ?? string.Empty,
                Sku = i.SellerSku ?? string.Empty,
                Quantity = i.QuantityOrdered,
                UnitPrice = decimal.TryParse(i.ItemPrice?.Amount, out var price) ? price : 0,
                TotalPrice = decimal.TryParse(i.ItemPrice?.Amount, out var t)
                                        ? t * i.QuantityOrdered : 0
            }).ToList() ?? [],
            ShippingAddress = o.ShippingAddress is null ? null : new ExternalAddress
            {
                Street = o.ShippingAddress.AddressLine1,
                City = o.ShippingAddress.City,
                Country = o.ShippingAddress.CountryCode,
                PostalCode = o.ShippingAddress.PostalCode
            },
            CreatedAt = o.PurchaseDate,
            UpdatedAt = o.LastUpdateDate
        };

        private static string MapFromAmazonOrderStatus(string amazonStatus) =>
            amazonStatus switch
            {
                "Pending" => "pending",
                "Unshipped" => "confirmed",
                "PartiallyShipped" => "processing",
                "Shipped" => "shipped",
                "InvoiceUnconfirmed" => "processing",
                "Canceled" => "cancelled",
                "Unfulfillable" => "cancelled",
                _ => amazonStatus.ToLower()
            };
    }

    // ── Amazon API Models ─────────────────────────────────────────────────────

    internal class AmazonTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? TokenType { get; set; }
    }

    // — Catalog —
    internal class AmazonCatalogResponse
    {
        public List<AmazonCatalogItem>? Items { get; set; }
        public AmazonPagination? Pagination { get; set; }
    }

    internal class AmazonPagination
    {
        public string? NextToken { get; set; }
        public string? PreviousToken { get; set; }
    }

    internal class AmazonCatalogItem
    {
        public string? Asin { get; set; }
        public List<AmazonItemSummary>? Summaries { get; set; }
        public Dictionary<string, object>? Attributes { get; set; }
        public List<AmazonIdentifierGroup>? Identifiers { get; set; }
        public List<AmazonImageGroup>? Images { get; set; }
        public List<AmazonSalesRank>? SalesRanks { get; set; }
    }

    internal class AmazonItemSummary
    {
        public string? MarketplaceId { get; set; }
        public string? ItemName { get; set; }
        public string? Status { get; set; }
        public string? BrandName { get; set; }
    }

    internal class AmazonIdentifierGroup
    {
        public string? MarketplaceId { get; set; }
        public List<AmazonIdentifier>? Identifiers { get; set; }
    }

    internal class AmazonIdentifier
    {
        public string? IdentifierType { get; set; }
        public string? Identifier { get; set; }
    }

    internal class AmazonImageGroup
    {
        public string? Variant { get; set; }
        public List<AmazonImage>? Images { get; set; }
    }

    internal class AmazonImage
    {
        public string? Link { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
    }

    internal class AmazonSalesRank
    {
        public string? MarketplaceId { get; set; }
        public string? Title { get; set; }
        public int Rank { get; set; }
    }

    // — Orders —
    internal class AmazonOrdersResponse
    {
        public AmazonOrdersPayload? Payload { get; set; }
    }

    internal class AmazonOrdersPayload
    {
        public List<AmazonOrder>? Orders { get; set; }
        public string? NextToken { get; set; }
    }

    internal class AmazonSingleOrderResponse
    {
        public AmazonOrder? Payload { get; set; }
    }

    internal class AmazonOrder
    {
        public string AmazonOrderId { get; set; } = string.Empty;
        public string? SellerOrderId { get; set; }
        public string? OrderStatus { get; set; }
        public AmazonMoney? OrderTotal { get; set; }
        public AmazonBuyerInfo? BuyerInfo { get; set; }
        public AmazonAddress? ShippingAddress { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime? LastUpdateDate { get; set; }
        public List<AmazonOrderItem>? Items { get; set; }
    }

    internal class AmazonBuyerInfo
    {
        public string? BuyerEmail { get; set; }
        public string? BuyerName { get; set; }
    }

    internal class AmazonMoney
    {
        public string? Amount { get; set; }
        public string? CurrencyCode { get; set; }
    }

    internal class AmazonAddress
    {
        public string? AddressLine1 { get; set; }
        public string? City { get; set; }
        public string? CountryCode { get; set; }
        public string? PostalCode { get; set; }
    }

    // — Order Items —
    internal class AmazonOrderItemsResponse
    {
        public AmazonOrderItemsPayload? Payload { get; set; }
    }

    internal class AmazonOrderItemsPayload
    {
        public List<AmazonOrderItem>? OrderItems { get; set; }
    }

    internal class AmazonOrderItem
    {
        public string? Asin { get; set; }
        public string? SellerSku { get; set; }
        public string? Title { get; set; }
        public int QuantityOrdered { get; set; }
        public AmazonMoney? ItemPrice { get; set; }
    }

    // — Inventory —
    internal class AmazonInventoryResponse
    {
        public AmazonInventoryPayload? Payload { get; set; }
    }

    internal class AmazonInventoryPayload
    {
        public List<AmazonInventorySummary>? InventorySummaries { get; set; }
        public string? NextToken { get; set; }
    }

    internal class AmazonInventorySummary
    {
        public string? Asin { get; set; }
        public string? SellerSku { get; set; }
        public int TotalQuantity { get; set; }
    }

    // — Notifications —
    internal class AmazonSubscriptionsResponse
    {
        public AmazonSubscriptionPayload? Payload { get; set; }
    }

    internal class AmazonSubscriptionPayload
    {
        public string? SubscriptionId { get; set; }
    }
}