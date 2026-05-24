using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Infrastructure.Adapters.Jarir
{
    /// <summary>
    /// Jarir Bookstore Marketplace Adapter.
    /// Jarir does not expose a public third-party seller/marketplace API.
    /// All methods return NOT_SUPPORTED (501).
    /// Vendor onboarding is handled directly through Jarir's internal vendor portal.
    /// </summary>
    public class JarirAdapter : IMarketplaceAdapter
    {
        public MarketplacePlatform Platform => MarketplacePlatform.Jarir;

        public AdapterCapabilities Capabilities => new()
        {
            SupportsProducts = false,
            SupportsOrders = false,
            SupportsCustomers = false,
            SupportsInventory = false,
            SupportsPrices = false,
            SupportsWebhooks = false,
            SupportsOAuth = false,
            SupportsApiKey = false,
            SupportsBulkSync = false,
            SupportsRealTimeSync = false,
            SupportedSyncDirections = [],
            SupportedEntityTypes = []
        };

        // ── Connection ───────────────────────────────────────────────────────

        public Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<TokenData>());

        // ── Products ─────────────────────────────────────────────────────────

        public Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<IReadOnlyList<ExternalProduct>>());

        public Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<ExternalProduct>());

        public Task<AdapterResult<string>> CreateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<string>());

        public Task<AdapterResult> UpdateProductAsync(
            StoreIntegration integration,
            ExternalProduct product,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        public Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        // ── Orders ───────────────────────────────────────────────────────────

        public Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration,
            SyncFilter? filter = null,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<IReadOnlyList<ExternalOrder>>());

        public Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration,
            string externalId,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<ExternalOrder>());

        public Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration,
            string externalId,
            string newStatus,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        // ── Inventory ────────────────────────────────────────────────────────

        public Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported<IReadOnlyList<ExternalInventory>>());

        public Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration,
            IReadOnlyList<ExternalInventory> items,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        // ── Webhooks ─────────────────────────────────────────────────────────

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration,
            IReadOnlyList<string> eventTypes,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration,
            CancellationToken ct = default)
            => Task.FromResult(NotSupported());

        public bool VerifyWebhookSignature(
            StoreIntegration integration,
            string payload,
            string signature)
            => false;

        // ── Helpers ──────────────────────────────────────────────────────────

        private static AdapterResult NotSupported() =>
            AdapterResult.Failure(
                "Jarir Bookstore does not provide a public marketplace API. " +
                "Please contact Jarir's vendor team directly for integration arrangements.",
                "NOT_SUPPORTED",
                statusCode: 501);

        private static AdapterResult<T> NotSupported<T>() =>
            AdapterResult<T>.Failure(
                "Jarir Bookstore does not provide a public marketplace API. " +
                "Please contact Jarir's vendor team directly for integration arrangements.",
                "NOT_SUPPORTED",
                statusCode: 501);
    }
}