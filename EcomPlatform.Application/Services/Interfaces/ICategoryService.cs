using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Categories;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<ApiResponse<CategoryResponseDto>> CreateAsync(CreateCategoryDto dto);
        Task<ApiResponse<CategoryResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<CategoryResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<CategoryResponseDto>> UpdateAsync(Guid id, UpdateCategoryDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
    }
}