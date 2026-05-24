using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class CreateIntegrationDto
    {
        [Required]
        public MarketplacePlatform Platform { get; init; }

        [Required, MaxLength(100)]
        public string DisplayName { get; init; } = string.Empty;

        // ── Auth ─────────────────────────────────────────────────────────────
        public string? ApiKey { get; init; }
        public string? ApiSecret { get; init; }
        public string? StoreUrl { get; init; }
        public string? ExternalStoreId { get; init; }

        // ── Sync Settings ────────────────────────────────────────────────────
        public SyncDirection SyncDirection { get; init; } = SyncDirection.BiDirectional;
        public bool SyncProducts { get; init; } = true;
        public bool SyncOrders { get; init; } = true;
        public bool SyncCustomers { get; init; } = true;
        public bool SyncInventory { get; init; } = true;
        public bool SyncPrices { get; init; } = true;
        public int AutoSyncIntervalMinutes { get; init; } = 0;
    }
}