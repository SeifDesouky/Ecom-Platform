using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Settings;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ISettingService
    {
        Task<ApiResponse<SettingResponseDto>> CreateAsync(CreateSettingDto dto);
        Task<ApiResponse<SettingResponseDto>> GetByKeyAsync(string key, Guid? tenantId);
        Task<ApiResponse<IEnumerable<SettingGroupDto>>> GetAllByTenantAsync(Guid? tenantId);
        Task<ApiResponse<SettingResponseDto>> UpdateAsync(string key, UpdateSettingDto dto, Guid? tenantId);
        Task<ApiResponse<bool>> BulkUpdateAsync(BulkUpdateSettingDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> InitializeDefaultSettingsAsync(Guid tenantId);

        // ✅ SuperAdmin فقط
        Task<ApiResponse<IEnumerable<SettingGroupDto>>> GetPlatformSettingsAsync();
        Task<ApiResponse<bool>> BulkUpdatePlatformSettingsAsync(BulkUpdateSettingDto dto);
    }
}