using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tenants;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ITenantService
    {
        Task<ApiResponse<TenantResponseDto>> CreateAsync(CreateTenantDto dto);
        Task<ApiResponse<TenantResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<TenantResponseDto>>> GetAllAsync();
        Task<ApiResponse<TenantResponseDto>> UpdateAsync(Guid id, UpdateTenantDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
    }
}