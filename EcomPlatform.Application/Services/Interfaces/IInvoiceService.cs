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
        Task<ApiResponse<IEnumerable<InvoiceResponseDto>>> GetAllByTenantAsync(Guid tenantId);
        Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, InvoiceStatus status);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
    }
}