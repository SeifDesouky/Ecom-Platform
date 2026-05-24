using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IMarketplaceAdapter
    {
        MarketplacePlatform Platform { get; }
        AdapterCapabilities Capabilities { get; }

        // ── Connection ───────────────────────────────────────────────────────
        Task<AdapterResult> TestConnectionAsync(StoreIntegration integration, CancellationToken ct = default);
        Task<AdapterResult<TokenData>> RefreshTokenAsync(StoreIntegration integration, CancellationToken ct = default);

        // ── Products ─────────────────────────────────────────────────────────
        Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(StoreIntegration integration, SyncFilter? filter = null, CancellationToken ct = default);
        Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(StoreIntegration integration, string externalId, CancellationToken ct = default);
        Task<AdapterResult<string>> CreateProductAsync(StoreIntegration integration, ExternalProduct product, CancellationToken ct = default);
        Task<AdapterResult> UpdateProductAsync(StoreIntegration integration, ExternalProduct product, CancellationToken ct = default);
        Task<AdapterResult> DeleteProductAsync(StoreIntegration integration, string externalId, CancellationToken ct = default);

        // ── Orders ───────────────────────────────────────────────────────────
        Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(StoreIntegration integration, SyncFilter? filter = null, CancellationToken ct = default);
        Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(StoreIntegration integration, string externalId, CancellationToken ct = default);
        Task<AdapterResult> UpdateOrderStatusAsync(StoreIntegration integration, string externalId, string newStatus, CancellationToken ct = default);

        // ── Inventory ────────────────────────────────────────────────────────
        Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(StoreIntegration integration, CancellationToken ct = default);
        Task<AdapterResult> UpdateInventoryAsync(StoreIntegration integration, IReadOnlyList<ExternalInventory> items, CancellationToken ct = default);

        // ── Webhooks ─────────────────────────────────────────────────────────
        Task<AdapterResult> RegisterWebhooksAsync(StoreIntegration integration, IReadOnlyList<string> eventTypes, CancellationToken ct = default);
        Task<AdapterResult> UnregisterWebhooksAsync(StoreIntegration integration, CancellationToken ct = default);
        bool VerifyWebhookSignature(StoreIntegration integration, string payload, string signature);
    }
}