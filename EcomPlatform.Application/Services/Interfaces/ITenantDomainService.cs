using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Domains;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface ITenantDomainService
    {
        Task<ApiResponse<TenantDomainResponseDto>> AddDomainAsync(CreateTenantDomainDto dto);
        Task<ApiResponse<IEnumerable<TenantDomainResponseDto>>> GetByTenantAsync(Guid tenantId);
        Task<ApiResponse<TenantDomainResponseDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<bool>> VerifyDomainAsync(Guid id);
        Task<ApiResponse<bool>> EnableSSLAsync(Guid id);
        Task<ApiResponse<bool>> SetPrimaryAsync(Guid id);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, DomainStatus status);

        // Super Admin
        Task<ApiResponse<IEnumerable<TenantDomainResponseDto>>> GetAllDomainsAsync(DomainStatus? status = null);
    }
}