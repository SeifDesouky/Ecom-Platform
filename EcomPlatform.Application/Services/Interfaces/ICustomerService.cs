using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Customers;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<ApiResponse<CustomerResponseDto>> CreateAsync(CreateCustomerDto dto);
        Task<ApiResponse<CustomerResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<PagedResponse<CustomerResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination);
        Task<ApiResponse<CustomerResponseDto>> UpdateAsync(Guid id, UpdateCustomerDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> ToggleStatusAsync(Guid id);
        Task<ApiResponse<CustomerAddressResponseDto>> AddAddressAsync(CreateCustomerAddressDto dto);
        Task<ApiResponse<bool>> DeleteAddressAsync(Guid addressId);
        Task<ApiResponse<bool>> SetDefaultAddressAsync(Guid addressId);
    }
}