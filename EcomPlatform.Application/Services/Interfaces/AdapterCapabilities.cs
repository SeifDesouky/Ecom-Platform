using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Integrations
{
    public class AdapterCapabilities
    {
        public bool SupportsProducts { get; init; }
        public bool SupportsOrders { get; init; }
        public bool SupportsCustomers { get; init; }
        public bool SupportsInventory { get; init; }
        public bool SupportsPrices { get; init; }
        public bool SupportsWebhooks { get; init; }
        public bool SupportsOAuth { get; init; }
        public bool SupportsApiKey { get; init; }
        public bool SupportsBulkSync { get; init; }
        public bool SupportsRealTimeSync { get; init; }
        public IReadOnlyList<SyncDirection> SupportedSyncDirections { get; init; } = [];
        public IReadOnlyList<SyncEntityType> SupportedEntityTypes { get; init; } = [];
    }
}