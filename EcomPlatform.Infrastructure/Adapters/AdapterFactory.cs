using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Infrastructure.Adapters
{
    public class AdapterFactory : IAdapterFactory
    {
        private readonly IReadOnlyDictionary<MarketplacePlatform, IMarketplaceAdapter> _adapters;

        public AdapterFactory(IEnumerable<IMarketplaceAdapter> adapters)
        {
            _adapters = adapters.ToDictionary(a => a.Platform);
        }

        public IMarketplaceAdapter GetAdapter(MarketplacePlatform platform)
        {
            if (!_adapters.TryGetValue(platform, out var adapter))
                throw new NotSupportedException($"No adapter registered for platform: {platform}");

            return adapter;
        }

        public bool IsSupported(MarketplacePlatform platform) =>
            _adapters.ContainsKey(platform);

        public IReadOnlyList<MarketplacePlatform> GetSupportedPlatforms() =>
            _adapters.Keys.ToList();
    }
}