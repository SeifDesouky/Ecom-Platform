using EcomPlatform.Application.DTOs.Zatca;
using EcomPlatform.Application.Common;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IZatcaService
    {
        Task<ApiResponse<ZatcaInvoiceDto>> GenerateZatcaInvoiceAsync(Guid invoiceId);
        Task<ApiResponse<ZatcaSubmissionDto>> SubmitInvoiceAsync(Guid invoiceId);
        Task<ApiResponse<ZatcaSubmissionDto>> CheckComplianceAsync(Guid invoiceId);
        ZatcaCsrDto GenerateCsr(string commonName, string organizationName,
            string organizationalUnit, string countryCode, string vatNumber);
        Task<ApiResponse<ZatcaOnboardingDto>> OnboardAsync(ZatcaOnboardingRequestDto request);
    }
}