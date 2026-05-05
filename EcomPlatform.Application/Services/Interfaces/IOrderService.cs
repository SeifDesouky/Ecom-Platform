using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Orders;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IOrderService
    {
        Task<ApiResponse<OrderResponseDto>> CreateAsync(CreateOrderDto dto);
        Task<ApiResponse<OrderResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<IEnumerable<OrderResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, OrderStatus status);
        Task<ApiResponse<bool>> UpdatePaymentStatusAsync(Guid id, PaymentStatus status);
        Task<ApiResponse<bool>> CancelOrderAsync(Guid id);
    }
}