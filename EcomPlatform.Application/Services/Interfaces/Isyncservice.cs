using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ISyncService
    {
        Task<SyncResultDto> SyncAsync(
            StoreIntegration integration,
            SyncEntityType entityType,
            SyncDirection direction,
            bool isManual = true,
            CancellationToken ct = default);
    }
}