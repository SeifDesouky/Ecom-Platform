using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Shipping;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IShippingService
    {
        Task<ApiResponse<ShippingZoneResponseDto>> CreateZoneAsync(CreateShippingZoneDto dto);
        Task<ApiResponse<IEnumerable<ShippingZoneResponseDto>>> GetZonesByTenantAsync(Guid tenantId);
        Task<ApiResponse<ShippingZoneResponseDto>> GetZoneByIdAsync(Guid id);
        Task<ApiResponse<bool>> DeleteZoneAsync(Guid id);
        Task<ApiResponse<bool>> ToggleZoneStatusAsync(Guid id);
        Task<ApiResponse<ShippingMethodResponseDto>> CreateMethodAsync(CreateShippingMethodDto dto);
        Task<ApiResponse<bool>> DeleteMethodAsync(Guid id);
        Task<ApiResponse<bool>> ToggleMethodStatusAsync(Guid id);
        Task<ApiResponse<IEnumerable<ShippingMethodResponseDto>>> CalculateShippingAsync(CalculateShippingDto dto);
    }
}