using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Users;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IUserService
    {
        Task<ApiResponse<UserResponseDto>> CreateAsync(CreateUserDto dto);
        Task<ApiResponse<UserResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<UserResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<UserResponseDto>> UpdateAsync(Guid id, UpdateUserDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
        Task<ApiResponse<bool>> ChangeRoleAsync(Guid id, UserRole role);
        Task<ApiResponse<bool>> ResetPasswordAsync(Guid id, string newPassword);
    }
}