using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Integrations;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IIntegrationService
    {
        // ── CRUD ─────────────────────────────────────────────────────────────
        Task<ApiResponse<IntegrationDto>> CreateAsync(CreateIntegrationDto dto, Guid tenantId, CancellationToken ct = default);
        Task<ApiResponse<IntegrationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<IntegrationDto>>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
        Task<ApiResponse<IntegrationDto>> UpdateAsync(Guid id, UpdateIntegrationDto dto, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeleteAsync(Guid id, CancellationToken ct = default);

        // ── Connection ───────────────────────────────────────────────────────
        Task<ApiResponse<AdapterResult>> TestConnectionAsync(Guid integrationId, CancellationToken ct = default);
        Task<ApiResponse<bool>> ActivateAsync(Guid integrationId, CancellationToken ct = default);
        Task<ApiResponse<bool>> DeactivateAsync(Guid integrationId, CancellationToken ct = default);

        // ── Sync ─────────────────────────────────────────────────────────────
        Task<ApiResponse<SyncResultDto>> SyncAsync(Guid integrationId, SyncEntityType entityType, SyncDirection direction, bool isManual = true, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<SyncLogDto>>> GetSyncLogsAsync(Guid integrationId, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<IntegrationDto>>> GetAllAsync(CancellationToken ct = default);
    }
}