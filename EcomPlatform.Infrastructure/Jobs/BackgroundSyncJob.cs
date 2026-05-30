using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;
using EcomPlatform.Infrastructure.Data;
using EcomPlatform.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcomPlatform.Infrastructure.Jobs
{
    /// <summary>
    /// Hosted service — يعمل sync تلقائي لكل الـ integrations النشطة كل X دقيقة.
    /// الـ interval بيتحدد من appsettings.json → "SyncSettings:IntervalMinutes"
    /// </summary>
    public sealed class BackgroundSyncJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundSyncJob> _logger;
        private readonly TimeSpan _interval;

        public BackgroundSyncJob(
            IServiceScopeFactory scopeFactory,
            ILogger<BackgroundSyncJob> logger,
            IOptions<SyncSettings> syncOptions)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(syncOptions.Value.IntervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[BackgroundSyncJob] Started — interval: {Interval} min",
                _interval.TotalMinutes);

            // delay صغير عشان الـ app يخلص startup
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunSyncCycleAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunSyncCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("[BackgroundSyncJob] Cycle started at {Time}", DateTime.UtcNow);

            try
            {
                var activeIds = await GetActiveIntegrationIdsAsync(ct);

                if (!activeIds.Any())
                {
                    _logger.LogDebug("[BackgroundSyncJob] No active integrations found.");
                    return;
                }

                var entityTypes = new[]
                {
                    SyncEntityType.Products,
                    SyncEntityType.Orders,
                    SyncEntityType.Inventory,
                };

                // ✅ FIX: scope منفصل لكل integration + entity type بدل Task.WhenAll
                foreach (var id in activeIds)
                {
                    foreach (var entityType in entityTypes)
                    {
                        if (ct.IsCancellationRequested) return;

                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var integrationService = scope.ServiceProvider
                            .GetRequiredService<IIntegrationService>();

                        await SyncOneAsync(integrationService, id, entityType, ct);
                    }
                }

                _logger.LogInformation(
                    "[BackgroundSyncJob] Cycle done — {Count} integrations × {Types} types",
                    activeIds.Count, entityTypes.Length);
            }
            catch (OperationCanceledException)
            {
                // App بيقفل — طبيعي
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BackgroundSyncJob] Unhandled error in sync cycle");
            }
        }

        private static async Task SyncOneAsync(
            IIntegrationService service,
            Guid integrationId,
            SyncEntityType entityType,
            CancellationToken ct)
        {
            await service.SyncAsync(
                integrationId,
                entityType,
                SyncDirection.Import,
                isManual: false,
                ct);
        }

        private async Task<IReadOnlyList<Guid>> GetActiveIntegrationIdsAsync(
            CancellationToken ct)
        {
            // ✅ FIX: scope منفصل للقراءة
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.Set<EcomPlatform.Core.Entities.StoreIntegration>()
                .Where(x => x.Status == IntegrationStatus.Active && !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(ct);
        }
    }
}