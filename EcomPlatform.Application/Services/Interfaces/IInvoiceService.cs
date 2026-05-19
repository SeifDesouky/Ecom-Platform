using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Invoices;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IInvoiceService
    {
        Task<ApiResponse<InvoiceResponseDto>> GenerateFromOrderAsync(Guid orderId);
        Task<ApiResponse<InvoiceResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<InvoiceResponseDto>> GetByOrderIdAsync(Guid orderId);
        Task<ApiResponse<PagedResponse<InvoiceResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination);
        Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, InvoiceStatus status);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<InvoiceResponseDto>> GetByCustomerAsync(Guid id, Guid customerId);
        Task<ApiResponse<PagedResponse<InvoiceResponseDto>>> GetAllByCustomerAsync(Guid customerId, PaginationParams pagination);

    }
}