using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Coupons;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ICouponService
    {
        Task<ApiResponse<CouponResponseDto>> CreateAsync(CreateCouponDto dto);
        Task<ApiResponse<CouponResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<CouponResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
        Task<ApiResponse<CouponValidationResponseDto>> ValidateAsync(ValidateCouponDto dto);
    }
}