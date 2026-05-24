using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Infrastructure.Adapters.Salla.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcomPlatform.Infrastructure.Adapters.Salla
{
    public class SallaAdapter : IMarketplaceAdapter
    {
        private const string BaseUrl = "https://api.salla.dev/admin/v2";

        private readonly HttpClient _httpClient;
        private readonly SallaAuthService _authService;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public MarketplacePlatform Platform => MarketplacePlatform.Salla;

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

        public SallaAdapter(
            HttpClient httpClient,
            SallaAuthService authService,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _authService = authService;
            _clientId = configuration["Salla:ClientId"] ?? string.Empty;
            _clientSecret = configuration["Salla:ClientSecret"] ?? string.Empty;
        }

        // ── Connection ───────────────────────────────────────────────────────

        public async Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeader(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/store/info", ct);

                if (response.IsSuccessStatusCode)
                    return AdapterResult.Success();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return AdapterResult.Failure("Invalid or expired token", "UNAUTHORIZED", 401);

                return AdapterResult.Failure($"Connection failed: {response.StatusCode}",
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
            return await _authService.RefreshAccessTokenAsync(
                integration, _clientId, _clientSecret, ct);
        }

        // ── Products ─────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeader(integration);

                var url = $"{BaseUrl}/products?per_page={filter?.PageSize ?? 50}&page={filter?.Page ?? 1}";
                if (filter?.ModifiedAfter != null)
                    url += $"&updated_after={filter.ModifiedAfter:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(
                        $"Failed to get products: {content}",
                        statusCode: (int)response.StatusCode);

                var sallaResponse = JsonSerializer.Deserialize<SallaApiResponse<IReadOnlyList<SallaProduct>>>(content);
                var products = sallaResponse?.Data?.Select(MapToExternalProduct).ToList()
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
                SetAuthHeader(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/products/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalProduct>.Failure(
                        "Product not found", statusCode: (int)response.StatusCode);

                var sallaResponse = JsonSerializer.Deserialize<SallaApiResponse<SallaProduct>>(content);
                if (sallaResponse?.Data == null)
                    return AdapterResult<ExternalProduct>.Failure("Failed to parse product");

                return AdapterResult<ExternalProduct>.Success(
                    MapToExternalProduct(sallaResponse.Data));
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
                SetAuthHeader(integration);

                // Salla بيتطلب: name, price, quantity, status
                var body = new
                {
                    name = product.Name,
                    description = product.Description ?? string.Empty,
                    sku = product.Sku ?? string.Empty,
                    price = new { amount = product.Price, currency = "SAR" },
                    quantity = product.StockQuantity,
                    status = product.IsActive ? "sale" : "hidden",
                    with_tax = false
                };

                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/products", request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<string>.Failure(
                        $"Failed to create product: {content}",
                        statusCode: (int)response.StatusCode);

                var sallaResponse = JsonSerializer.Deserialize<SallaApiResponse<SallaProduct>>(content);
                var createdId = sallaResponse?.Data?.Id.ToString();

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
            // TODO: implement when Export direction needed
            return await Task.FromResult(
                AdapterResult.Failure("Update product not implemented yet"));
        }

        public async Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
        {
            // TODO: implement when Export direction needed
            return await Task.FromResult(
                AdapterResult.Failure("Delete product not implemented yet"));
        }

        // ── Orders ───────────────────────────────────────────────────────────

        public async Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
        {
            try
            {
                SetAuthHeader(integration);

                var url = $"{BaseUrl}/orders?per_page={filter?.PageSize ?? 50}&page={filter?.Page ?? 1}";

                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(
                        $"Failed to get orders: {content}",
                        statusCode: (int)response.StatusCode);

                var sallaResponse = JsonSerializer.Deserialize<SallaApiResponse<IReadOnlyList<SallaOrder>>>(content);
                var orders = sallaResponse?.Data?.Select(MapToExternalOrder).ToList()
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
                SetAuthHeader(integration);
                var response = await _httpClient.GetAsync($"{BaseUrl}/orders/{externalId}", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    return AdapterResult<ExternalOrder>.Failure(
                        "Order not found", statusCode: (int)response.StatusCode);

                var sallaResponse = JsonSerializer.Deserialize<SallaApiResponse<SallaOrder>>(content);
                if (sallaResponse?.Data == null)
                    return AdapterResult<ExternalOrder>.Failure("Failed to parse order");

                return AdapterResult<ExternalOrder>.Success(
                    MapToExternalOrder(sallaResponse.Data));
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
                SetAuthHeader(integration);

                // Salla بيقبل status كـ string في الـ body
                var body = new { status = MapToSallaOrderStatus(newStatus) };
                var json = JsonSerializer.Serialize(body, _json);
                var request = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(
                    $"{BaseUrl}/orders/{externalId}/status", request, ct);
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
                SetAuthHeader(integration);

                var errors = new List<string>();

                foreach (var item in items)
                {
                    // Salla بيحدث الـ quantity per product
                    var body = new { quantity = item.Quantity };
                    var json = JsonSerializer.Serialize(body, _json);
                    var request = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.PutAsync(
                        $"{BaseUrl}/products/{item.ExternalProductId}/quantities",
                        request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync(ct);
                        errors.Add($"Product {item.ExternalProductId}: {content}");
                    }
                }

                if (errors.Count > 0)
                    return AdapterResult.Failure(
                        $"Some inventory updates failed: {string.Join(", ", errors)}");

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
            return await Task.FromResult(AdapterResult.Success());
        }

        public async Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
        {
            return await Task.FromResult(AdapterResult.Success());
        }

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
        {
            if (string.IsNullOrEmpty(integration.WebhookSecret))
                return false;

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(integration.WebhookSecret));

            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLower();

            return expected == signature.ToLower();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void SetAuthHeader(StoreIntegration integration)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", integration.ApiKey);
        }

        /// <summary>
        /// يحول الـ local OrderStatus لـ Salla status string
        /// </summary>
        private static string MapToSallaOrderStatus(string localStatus) =>
            localStatus.ToLower() switch
            {
                "pending" => "pending",
                "processing" => "in_progress",
                "shipped" => "shipping",
                "delivered" => "delivered",
                "cancelled" => "cancelled",
                "returned" => "returned",
                _ => localStatus
            };

        // ── Mapping ──────────────────────────────────────────────────────────

        private static ExternalProduct MapToExternalProduct(SallaProduct p) => new()
        {
            ExternalId = p.Id.ToString(),
            Name = p.Name,
            Description = p.Description,
            Sku = p.Sku,
            Price = p.Price?.Amount ?? 0,
            StockQuantity = p.Quantity,
            IsActive = p.Status == "sale",
            ImageUrl = p.Images?.FirstOrDefault(i => i.IsMain)?.Url
                         ?? p.Images?.FirstOrDefault()?.Url,
            Categories = p.Categories?.Select(c => c.Name).ToList() ?? [],
            Variants = p.Variants?.Select(v => new ExternalProductVariant
            {
                ExternalId = v.Id.ToString(),
                Sku = v.Sku,
                Price = v.Price?.Amount ?? 0,
                StockQuantity = v.Quantity,
                Options = v.Options?.ToDictionary(o => o.Name, o => o.Value)
                             ?? new Dictionary<string, string>()
            }).ToList() ?? [],
            UpdatedAt = p.UpdatedAt
        };

        private static ExternalOrder MapToExternalOrder(SallaOrder o) => new()
        {
            ExternalId = o.Id.ToString(),
            OrderNumber = o.ReferenceId,
            Status = o.Status?.Id ?? string.Empty,
            TotalAmount = o.Amounts?.Total?.Amount ?? 0,
            Currency = o.Amounts?.Currency ?? "SAR",
            Customer = o.Customer == null ? null : new ExternalCustomerInfo
            {
                ExternalId = o.Customer.Id.ToString(),
                Name = o.Customer.Name,
                Email = o.Customer.Email,
                Phone = o.Customer.Mobile
            },
            Items = o.Items?.Select(i => new ExternalOrderItem
            {
                ExternalProductId = i.Product?.Id.ToString() ?? string.Empty,
                ProductName = i.Product?.Name ?? string.Empty,
                Sku = i.Product?.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.Amounts?.Price?.Amount ?? 0,
                TotalPrice = i.Amounts?.Total?.Amount ?? 0
            }).ToList() ?? [],
            ShippingAddress = o.Shipping?.Address == null ? null : new ExternalAddress
            {
                Street = o.Shipping.Address.Street,
                City = o.Shipping.Address.City,
                Country = o.Shipping.Address.Country,
                PostalCode = o.Shipping.Address.PostalCode
            },
            CreatedAt = o.Date?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = o.Date?.UpdatedAt
        };
    }
}