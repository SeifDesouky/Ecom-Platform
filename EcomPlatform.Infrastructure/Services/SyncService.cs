using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;

namespace EcomPlatform.Infrastructure.Services
{
    public class SyncService : ISyncService
    {
        private readonly IAdapterFactory _adapterFactory;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<SyncService> _logger;

        public SyncService(
            IAdapterFactory adapterFactory,
            IUnitOfWork uow,
            ILogger<SyncService> logger)
        {
            _adapterFactory = adapterFactory;
            _uow = uow;
            _logger = logger;
        }

        public async Task<SyncResultDto> SyncAsync(
            StoreIntegration integration,
            SyncEntityType entityType,
            SyncDirection direction,
            bool isManual = true,
            CancellationToken ct = default)
        {
            var log = await StartSyncLogAsync(integration, entityType, direction, isManual);
            var startedAt = log.StartedAt;

            try
            {
                if (NeedsTokenRefresh(integration))
                    await RefreshTokenAsync(integration);

                var (success, failed, error) = entityType switch
                {
                    SyncEntityType.Products => await SyncProductsAsync(integration, direction, ct),
                    SyncEntityType.Orders => await SyncOrdersAsync(integration, direction, ct),
                    SyncEntityType.Inventory => await SyncInventoryAsync(integration, ct),
                    _ => (0, 0, (string?)$"{entityType} sync not supported yet")
                };

                return await CompleteSyncLogAsync(
                    log, integration, entityType, direction,
                    success, failed, error, startedAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Sync failed for integration {Id} entity {Entity}",
                    integration.Id, entityType);

                return await FailSyncLogAsync(
                    log, integration, entityType, direction,
                    ex.Message, startedAt);
            }
        }

        // ── Products ──────────────────────────────────────────────────────────

        private async Task<(int success, int failed, string? error)> SyncProductsAsync(
            StoreIntegration integration,
            SyncDirection direction,
            CancellationToken ct)
        {
            var adapter = _adapterFactory.GetAdapter(integration.Platform);
            int success = 0, failed = 0;

            if (direction is SyncDirection.Import or SyncDirection.BiDirectional)
            {
                var page = 1;
                while (true)
                {
                    var filter = new SyncFilter
                    {
                        ModifiedAfter = integration.LastSyncAt,
                        PageSize = 100,
                        Page = page
                    };

                    var fetchResult = await adapter.GetProductsAsync(integration, filter, ct);

                    if (!fetchResult.IsSuccess)
                        return (success, failed, fetchResult.ErrorMessage);

                    var products = fetchResult.Data ?? [];
                    if (products.Count == 0) break;

                    foreach (var external in products)
                    {
                        try
                        {
                            await UpsertProductAsync(integration, external);
                            success++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to upsert product {Id}: {Error}",
                                external.ExternalId, ex.Message);
                            failed++;
                        }
                    }

                    if (products.Count < filter.PageSize) break;
                    page++;
                }
            }

            return (success, failed, null);
        }

        private async Task UpsertProductAsync(
            StoreIntegration integration,
            ExternalProduct external)
        {
            var existing = await _uow.Products
                .FindByExternalIdAsync(external.ExternalId, integration.Id);

            if (existing is null)
            {
                var product = new Product
                {
                    TenantId = integration.TenantId,
                    Name = external.Name,
                    Description = external.Description ?? string.Empty,
                    ShortDescription = string.Empty,
                    Slug = string.Empty,
                    SKU = external.Sku ?? string.Empty,
                    Price = external.Price,
                    Stock = external.StockQuantity,
                    IsActive = external.IsActive,
                    ExternalId = external.ExternalId,
                    StoreIntegrationId = integration.Id,
                    Status = external.IsActive
                                            ? ProductStatus.Active
                                            : ProductStatus.Inactive,
                    CreatedAt = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(external.ImageUrl))
                {
                    product.Images.Add(new ProductImage
                    {
                        Url = external.ImageUrl,
                        IsMain = true,
                        Alt = external.Name
                    });
                }

                await _uow.Products.AddAsync(product);
            }
            else
            {
                existing.Name = external.Name;
                existing.Description = external.Description ?? string.Empty;
                existing.SKU = external.Sku ?? existing.SKU;
                existing.Price = external.Price;
                existing.Stock = external.StockQuantity;
                existing.IsActive = external.IsActive;
                existing.Status = external.IsActive
                                        ? ProductStatus.Active
                                        : ProductStatus.Inactive;
                existing.UpdatedAt = DateTime.UtcNow;

                await _uow.Products.UpdateAsync(existing);
            }

            await _uow.SaveChangesAsync();
        }

        // ── Orders ────────────────────────────────────────────────────────────

        private async Task<(int success, int failed, string? error)> SyncOrdersAsync(
            StoreIntegration integration,
            SyncDirection direction,
            CancellationToken ct)
        {
            var adapter = _adapterFactory.GetAdapter(integration.Platform);
            int success = 0, failed = 0;

            if (direction is SyncDirection.Import or SyncDirection.BiDirectional)
            {
                var page = 1;
                while (true)
                {
                    var filter = new SyncFilter
                    {
                        ModifiedAfter = integration.LastSyncAt,
                        PageSize = 50,
                        Page = page
                    };

                    var fetchResult = await adapter.GetOrdersAsync(integration, filter, ct);

                    if (!fetchResult.IsSuccess)
                        return (success, failed, fetchResult.ErrorMessage);

                    var orders = fetchResult.Data ?? [];
                    if (orders.Count == 0) break;

                    foreach (var external in orders)
                    {
                        try
                        {
                            await UpsertOrderAsync(integration, external);
                            success++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to upsert order {Id}: {Error}",
                                external.ExternalId, ex.Message);
                            failed++;
                        }
                    }

                    if (orders.Count < filter.PageSize) break;
                    page++;
                }
            }

            return (success, failed, null);
        }

        private async Task UpsertOrderAsync(
            StoreIntegration integration,
            ExternalOrder external)
        {
            var existing = await _uow.Orders
                .FindByExternalIdAsync(external.ExternalId, integration.Id);

            if (existing is null)
            {
                var order = new Order
                {
                    TenantId = integration.TenantId,
                    OrderNumber = external.OrderNumber ?? external.ExternalId,
                    ExternalOrderNumber = external.OrderNumber ?? string.Empty,
                    Status = MapOrderStatus(external.Status),
                    Total = external.TotalAmount,
                    ExternalId = external.ExternalId,
                    StoreIntegrationId = integration.Id,
                    CustomerName = external.Customer?.Name ?? string.Empty,
                    CustomerEmail = external.Customer?.Email ?? string.Empty,
                    CustomerPhone = external.Customer?.Phone ?? string.Empty,
                    ShippingAddress = external.ShippingAddress?.Street ?? string.Empty,
                    ShippingCity = external.ShippingAddress?.City ?? string.Empty,
                    ShippingCountry = external.ShippingAddress?.Country ?? string.Empty,
                    CreatedAt = external.CreatedAt,
                    Items = external.Items.Select(i => new OrderItem
                    {
                        ProductName = i.ProductName,
                        ProductSKU = i.Sku ?? string.Empty,
                        ExternalProductId = i.ExternalProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice
                    }).ToList()
                };

                await _uow.Orders.AddAsync(order);
            }
            else
            {
                existing.Status = MapOrderStatus(external.Status);
                existing.UpdatedAt = DateTime.UtcNow;

                await _uow.Orders.UpdateAsync(existing);
            }

            await _uow.SaveChangesAsync();
        }

        // ── Inventory ────────────────────────────────────────────────────────

        private async Task<(int success, int failed, string? error)> SyncInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct)
        {
            var adapter = _adapterFactory.GetAdapter(integration.Platform);
            int success = 0, failed = 0;

            var fetchResult = await adapter.GetInventoryAsync(integration, ct);
            if (!fetchResult.IsSuccess)
                return (0, 0, fetchResult.ErrorMessage);

            foreach (var item in fetchResult.Data ?? [])
            {
                try
                {
                    var product = await _uow.Products
                        .FindByExternalIdAsync(item.ExternalProductId, integration.Id);

                    if (product is null) continue;

                    product.Stock = item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;

                    await _uow.Products.UpdateAsync(product);
                    await _uow.SaveChangesAsync();
                    success++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to update inventory {Id}: {Error}",
                        item.ExternalProductId, ex.Message);
                    failed++;
                }
            }

            return (success, failed, null);
        }

        // ── Token Refresh ─────────────────────────────────────────────────────

        private static bool NeedsTokenRefresh(StoreIntegration integration) =>
            integration.TokenExpiresAt.HasValue &&
            integration.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(10);

        private async Task RefreshTokenAsync(StoreIntegration integration)
        {
            var adapter = _adapterFactory.GetAdapter(integration.Platform);
            var result = await adapter.RefreshTokenAsync(integration);

            if (!result.IsSuccess || result.Data is null)
            {
                _logger.LogWarning(
                    "Token refresh failed for integration {Id}: {Error}",
                    integration.Id, result.ErrorMessage);
                return;
            }

            integration.ApiKey = result.Data.AccessToken;
            integration.RefreshToken = result.Data.RefreshToken;
            integration.TokenExpiresAt = result.Data.ExpiresAt;

            // ✅ FIX: جلب النسخة الـ tracked من DB بدل استخدام الـ decrypted copy
            var trackedIntegration = await _uow.StoreIntegrations.GetByIdAsync(integration.Id);
            if (trackedIntegration is not null)
            {
                trackedIntegration.ApiKey = integration.ApiKey;
                trackedIntegration.RefreshToken = integration.RefreshToken;
                trackedIntegration.TokenExpiresAt = integration.TokenExpiresAt;
                await _uow.StoreIntegrations.UpdateAsync(trackedIntegration);
            }
            await _uow.SaveChangesAsync();
        }

        // ── SyncLog Helpers ───────────────────────────────────────────────────

        private async Task<SyncLog> StartSyncLogAsync(
            StoreIntegration integration,
            SyncEntityType entityType,
            SyncDirection direction,
            bool isManual)
        {
            var log = new SyncLog
            {
                StoreIntegrationId = integration.Id,
                TenantId = integration.TenantId,
                EntityType = entityType,
                Direction = direction,
                IsManual = isManual,
                Status = SyncStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };

            await _uow.SyncLogs.AddAsync(log);
            await _uow.SaveChangesAsync();
            return log;
        }

        private async Task<SyncResultDto> CompleteSyncLogAsync(
            SyncLog log,
            StoreIntegration integration,
            SyncEntityType entityType,
            SyncDirection direction,
            int success,
            int failed,
            string? error,
            DateTime startedAt)
        {
            var completedAt = DateTime.UtcNow;
            var duration = (completedAt - startedAt).TotalSeconds;

            log.Status = failed == 0 && error is null
                ? SyncStatus.Success
                : success > 0
                    ? SyncStatus.PartialSuccess
                    : SyncStatus.Failed;

            log.SuccessCount = success;
            log.FailedCount = failed;
            log.TotalRecords = success + failed;
            log.DurationSeconds = duration;
            log.CompletedAt = completedAt;
            log.ErrorMessage = error;

            await _uow.SyncLogs.UpdateAsync(log);

            // ✅ FIX: جلب النسخة الـ tracked من DB بدل استخدام الـ decrypted copy
            var trackedIntegration = await _uow.StoreIntegrations.GetByIdAsync(integration.Id);
            if (trackedIntegration is not null)
            {
                trackedIntegration.LastSyncAt = completedAt;
                trackedIntegration.ConsecutiveErrorCount = 0;
                trackedIntegration.LastErrorMessage = null;
                await _uow.StoreIntegrations.UpdateAsync(trackedIntegration);
            }
            await _uow.SaveChangesAsync();

            return new SyncResultDto
            {
                SyncLogId = log.Id,
                EntityType = entityType,
                Direction = direction,
                Status = log.Status,
                TotalRecords = log.TotalRecords,
                SuccessCount = success,
                FailedCount = failed,
                DurationSeconds = duration,
                ErrorMessage = error,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }

        private async Task<SyncResultDto> FailSyncLogAsync(
            SyncLog log,
            StoreIntegration integration,
            SyncEntityType entityType,
            SyncDirection direction,
            string errorMessage,
            DateTime startedAt)
        {
            var completedAt = DateTime.UtcNow;

            log.Status = SyncStatus.Failed;
            log.CompletedAt = completedAt;
            log.DurationSeconds = (completedAt - startedAt).TotalSeconds;
            log.ErrorMessage = errorMessage;

            await _uow.SyncLogs.UpdateAsync(log);

            // ✅ FIX: جلب النسخة الـ tracked من DB بدل استخدام الـ decrypted copy
            var trackedIntegration = await _uow.StoreIntegrations.GetByIdAsync(integration.Id);
            if (trackedIntegration is not null)
            {
                trackedIntegration.LastErrorMessage = errorMessage;
                trackedIntegration.ConsecutiveErrorCount += 1;

                if (trackedIntegration.ConsecutiveErrorCount >= 5)
                {
                    trackedIntegration.Status = IntegrationStatus.Error;
                    _logger.LogWarning(
                        "Integration {Id} suspended after {Count} consecutive errors",
                        trackedIntegration.Id, trackedIntegration.ConsecutiveErrorCount);
                }

                await _uow.StoreIntegrations.UpdateAsync(trackedIntegration);
            }
            await _uow.SaveChangesAsync();

            return new SyncResultDto
            {
                SyncLogId = log.Id,
                EntityType = entityType,
                Direction = direction,
                Status = SyncStatus.Failed,
                ErrorMessage = errorMessage,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static OrderStatus MapOrderStatus(string externalStatus) =>
            externalStatus.ToLower() switch
            {
                "pending" => OrderStatus.Pending,
                "confirmed" => OrderStatus.Confirmed,
                "processing" or "in_progress" => OrderStatus.Processing,
                "shipped" or "shipping" => OrderStatus.Shipped,
                "delivered" => OrderStatus.Delivered,
                "cancelled" => OrderStatus.Cancelled,
                "returned" => OrderStatus.Returned,
                _ => OrderStatus.Pending
            };
    }
}