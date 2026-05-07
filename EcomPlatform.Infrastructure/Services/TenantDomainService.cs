using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Domains;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class TenantDomainService : ITenantDomainService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TenantDomainService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<TenantDomainResponseDto>> AddDomainAsync(CreateTenantDomainDto dto)
        {
            var existing = await _unitOfWork.TenantDomains.FindAsync(d => d.Domain == dto.Domain);
            if (existing.Any())
                return ApiResponse<TenantDomainResponseDto>.Fail("Domain already exists");

            if (dto.IsPrimary)
            {
                var currentDomains = await _unitOfWork.TenantDomains.FindAsync(d => d.TenantId == dto.TenantId);
                foreach (var d in currentDomains)
                {
                    d.IsPrimary = false;
                    await _unitOfWork.TenantDomains.UpdateAsync(d);
                }
            }

            var domain = new TenantDomain
            {
                Domain = dto.Domain.ToLower().Trim(),
                IsPrimary = dto.IsPrimary,
                Status = DomainStatus.Pending,
                SSLEnabled = false,
                VerificationToken = GenerateVerificationToken(),
                TenantId = dto.TenantId
            };

            await _unitOfWork.TenantDomains.AddAsync(domain);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<TenantDomainResponseDto>.Ok(MapToDto(domain), "Domain added successfully");
        }

        public async Task<ApiResponse<IEnumerable<TenantDomainResponseDto>>> GetByTenantAsync(Guid tenantId)
        {
            var domains = await _unitOfWork.TenantDomains.FindAsync(d => d.TenantId == tenantId);
            return ApiResponse<IEnumerable<TenantDomainResponseDto>>.Ok(domains.Select(MapToDto));
        }

        public async Task<ApiResponse<TenantDomainResponseDto>> GetByIdAsync(Guid id)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<TenantDomainResponseDto>.Fail("Domain not found");

            return ApiResponse<TenantDomainResponseDto>.Ok(MapToDto(domain));
        }

        public async Task<ApiResponse<bool>> VerifyDomainAsync(Guid id)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<bool>.Fail("Domain not found");

            // In production: verify CNAME record
            domain.Status = DomainStatus.Active;
            domain.VerifiedAt = DateTime.UtcNow;

            await _unitOfWork.TenantDomains.UpdateAsync(domain);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Domain verified successfully");
        }

        public async Task<ApiResponse<bool>> EnableSSLAsync(Guid id)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<bool>.Fail("Domain not found");

            if (domain.Status != DomainStatus.Active)
                return ApiResponse<bool>.Fail("Domain must be verified before enabling SSL");

            domain.SSLEnabled = true;
            domain.SSLExpiryDate = DateTime.UtcNow.AddYears(1);

            await _unitOfWork.TenantDomains.UpdateAsync(domain);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "SSL enabled successfully");
        }

        public async Task<ApiResponse<bool>> SetPrimaryAsync(Guid id)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<bool>.Fail("Domain not found");

            if (domain.Status != DomainStatus.Active)
                return ApiResponse<bool>.Fail("Domain must be active to set as primary");

            var allDomains = await _unitOfWork.TenantDomains.FindAsync(d => d.TenantId == domain.TenantId);
            foreach (var d in allDomains)
            {
                d.IsPrimary = d.Id == id;
                await _unitOfWork.TenantDomains.UpdateAsync(d);
            }

            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Primary domain updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<bool>.Fail("Domain not found");

            if (domain.IsPrimary)
                return ApiResponse<bool>.Fail("Cannot delete primary domain");

            await _unitOfWork.TenantDomains.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Domain deleted successfully");
        }

        public async Task<ApiResponse<bool>> UpdateStatusAsync(Guid id, DomainStatus status)
        {
            var domain = await _unitOfWork.TenantDomains.GetByIdAsync(id);
            if (domain == null)
                return ApiResponse<bool>.Fail("Domain not found");

            domain.Status = status;
            await _unitOfWork.TenantDomains.UpdateAsync(domain);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Domain status updated successfully");
        }

        private static string GenerateVerificationToken() =>
            $"ecom-verify-{Guid.NewGuid().ToString()[..16]}";

        private static TenantDomainResponseDto MapToDto(TenantDomain domain) => new()
        {
            Id = domain.Id,
            Domain = domain.Domain,
            Status = domain.Status,
            IsPrimary = domain.IsPrimary,
            SSLEnabled = domain.SSLEnabled,
            SSLExpiryDate = domain.SSLExpiryDate,
            VerificationToken = domain.VerificationToken,
            VerifiedAt = domain.VerifiedAt,
            Notes = domain.Notes,
            TenantId = domain.TenantId,
            CreatedAt = domain.CreatedAt
        };
    }
}