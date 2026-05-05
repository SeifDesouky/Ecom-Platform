using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Products;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IProductService
    {
        Task<ApiResponse<ProductResponseDto>> CreateAsync(CreateProductDto dto);
        Task<ApiResponse<ProductResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<ProductResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<IEnumerable<ProductResponseDto>>> GetByCategoryAsync(Guid categoryId);
        Task<ApiResponse<ProductResponseDto>> UpdateAsync(Guid id, UpdateProductDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
        Task<ApiResponse<bool>> UpdateStockAsync(Guid id, int quantity);
    }
}