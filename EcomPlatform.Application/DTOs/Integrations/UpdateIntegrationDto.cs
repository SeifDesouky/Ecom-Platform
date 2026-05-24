using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class UpdateIntegrationDto
    {
        [MaxLength(100)]
        public string? DisplayName { get; init; }

        // ── Auth ─────────────────────────────────────────────────────────────
        public string? ApiKey { get; init; }
        public string? ApiSecret { get; init; }
        public string? StoreUrl { get; init; }
        public string? ExternalStoreId { get; init; }

        // ── Sync Settings ────────────────────────────────────────────────────
        public bool? SyncProducts { get; init; }
        public bool? SyncOrders { get; init; }
        public bool? SyncCustomers { get; init; }
        public bool? SyncInventory { get; init; }
        public bool? SyncPrices { get; init; }
        public int? AutoSyncIntervalMinutes { get; init; }
    }
}