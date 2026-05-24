using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class IntegrationDto
    {
        public Guid Id { get; init; }
        public MarketplacePlatform Platform { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public IntegrationStatus Status { get; init; }

        // ── Sync Settings ────────────────────────────────────────────────────
        public SyncDirection SyncDirection { get; init; }
        public bool SyncProducts { get; init; }
        public bool SyncOrders { get; init; }
        public bool SyncCustomers { get; init; }
        public bool SyncInventory { get; init; }
        public bool SyncPrices { get; init; }
        public int AutoSyncIntervalMinutes { get; init; }

        // ── Stats ────────────────────────────────────────────────────────────
        public DateTime? LastSyncAt { get; init; }
        public string? LastErrorMessage { get; init; }
        public int ConsecutiveErrorCount { get; init; }

        // ── Meta ─────────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}