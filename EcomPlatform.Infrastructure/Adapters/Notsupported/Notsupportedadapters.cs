using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Infrastructure.Adapters.NotSupported
{
    /// <summary>
    /// Placeholder adapter للمنصات اللي مش عندها Public API رسمي حتى دلوقتي:
    ///   - Matjar    (متجر)          — مفيش API عام
    ///   - Tagger    (تاجر)          — مفيش API عام
    ///   - Toggar    (تجار)          — مفيش API عام
    ///   - Shopini   (شوبيني)        — مفيش API عام
    ///   - Paycorn   (بايكورن ستور)  — مفيش API عام
    ///   - Makhazin  (مخازن)         — مفيش API عام
    ///
    /// لما أي منصة منهم يطلع API رسمي:
    ///   1. اعمل Adapter مستقل ليه
    ///   2. شيله من الـ NotSupportedPlatforms list
    ///   3. سجله في Program.cs
    /// </summary>
    public abstract class NotSupportedAdapterBase : IMarketplaceAdapter
    {
        private const string Msg = "This platform does not have a public API yet. " +
                                   "Integration will be available once an official API is released.";

        public abstract MarketplacePlatform Platform { get; }

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

        public Task<AdapterResult> TestConnectionAsync(
            StoreIntegration integration, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<TokenData>> RefreshTokenAsync(
            StoreIntegration integration, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<TokenData>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<IReadOnlyList<ExternalProduct>>> GetProductsAsync(
            StoreIntegration integration, SyncFilter? filter = null, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<IReadOnlyList<ExternalProduct>>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<ExternalProduct>> GetProductByIdAsync(
            StoreIntegration integration, string externalId, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<ExternalProduct>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<string>> CreateProductAsync(
            StoreIntegration integration, ExternalProduct product, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<string>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UpdateProductAsync(
            StoreIntegration integration, ExternalProduct product, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> DeleteProductAsync(
            StoreIntegration integration, string externalId, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<IReadOnlyList<ExternalOrder>>> GetOrdersAsync(
            StoreIntegration integration, SyncFilter? filter = null, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<IReadOnlyList<ExternalOrder>>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<ExternalOrder>> GetOrderByIdAsync(
            StoreIntegration integration, string externalId, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<ExternalOrder>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UpdateOrderStatusAsync(
            StoreIntegration integration, string externalId, string newStatus, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult<IReadOnlyList<ExternalInventory>>> GetInventoryAsync(
            StoreIntegration integration, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult<IReadOnlyList<ExternalInventory>>.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UpdateInventoryAsync(
            StoreIntegration integration, IReadOnlyList<ExternalInventory> items, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> RegisterWebhooksAsync(
            StoreIntegration integration, IReadOnlyList<string> eventTypes, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public Task<AdapterResult> UnregisterWebhooksAsync(
            StoreIntegration integration, CancellationToken ct = default) =>
            Task.FromResult(AdapterResult.Failure(Msg, "NOT_SUPPORTED", 501));

        public bool VerifyWebhookSignature(
            StoreIntegration integration, string payload, string signature) => false;
    }

    // ── Concrete Adapters ─────────────────────────────────────────────────────

    /// <summary>متجر — مفيش Public API</summary>
    public sealed class MatjarAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.Matjar;
    }

    /// <summary>تاجر — مفيش Public API</summary>
    public sealed class TaggerAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.Tagger;
    }

    /// <summary>تجار — مفيش Public API</summary>
    public sealed class ToggarAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.Toggar;
    }

    /// <summary>شوبيني — مفيش Public API</summary>
    public sealed class ShopiniAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.Shopini;
    }

    /// <summary>بايكورن ستور — مفيش Public API</summary>
    public sealed class PaycornStoreAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.PaycornStore;
    }

    /// <summary>مخازن الإلكترونية — مفيش Public API</summary>
    public sealed class MakhazinAdapter : NotSupportedAdapterBase
    {
        public override MarketplacePlatform Platform => MarketplacePlatform.Makhazin;
    }
}