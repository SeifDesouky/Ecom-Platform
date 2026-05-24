using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAdapterFactory
    {
        IMarketplaceAdapter GetAdapter(MarketplacePlatform platform);
        bool IsSupported(MarketplacePlatform platform);
        IReadOnlyList<MarketplacePlatform> GetSupportedPlatforms();
    }
}