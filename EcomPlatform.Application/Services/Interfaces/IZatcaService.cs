using EcomPlatform.Application.DTOs.Zatca;
using EcomPlatform.Application.Common;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IZatcaService
    {
        Task<ApiResponse<ZatcaInvoiceDto>> GenerateZatcaInvoiceAsync(Guid invoiceId);
    }
}