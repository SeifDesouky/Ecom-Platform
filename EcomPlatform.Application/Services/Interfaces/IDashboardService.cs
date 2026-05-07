using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Dashboard;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<ApiResponse<DashboardStatsDto>> GetTenantStatsAsync(Guid tenantId);
        Task<ApiResponse<DashboardStatsDto>> GetPlatformStatsAsync();
        Task<ApiResponse<DashboardStatsDto>> GetLatestSnapshotAsync(Guid? tenantId);
    }
}