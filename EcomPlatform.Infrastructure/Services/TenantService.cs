using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Tenants;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TenantService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<TenantResponseDto>> CreateAsync(CreateTenantDto dto)
        {
            // Check Slug unique
            var existing = await _unitOfWork.Tenants.FindAsync(t => t.Slug == dto.Slug);
            if (existing.Any())
                return ApiResponse<TenantResponseDto>.Fail("Slug already exists");

            // Check Email unique
            var existingEmail = await _unitOfWork.Tenants.FindAsync(t => t.Email == dto.Email);
            if (existingEmail.Any())
                return ApiResponse<TenantResponseDto>.Fail("Email already exists");

            var tenant = new Tenant
            {
                Name = dto.Name,
                Slug = dto.Slug,
                Email = dto.Email,
                Phone = dto.Phone,
                Logo = dto.Logo,
                Domain = dto.Domain,
                SubscriptionEndDate = dto.SubscriptionEndDate,
                VatNumber = dto.VatNumber,   // ضيف ده
                VatRate = dto.VatRate,
                IsActive = true,
                Status = TenantStatus.Active
            };

            await _unitOfWork.Tenants.AddAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<TenantResponseDto>.Ok(MapToDto(tenant), "Tenant created successfully");
        }

        public async Task<ApiResponse<TenantResponseDto>> GetByIdAsync(Guid id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
            if (tenant == null)
                return ApiResponse<TenantResponseDto>.Fail("Tenant not found");

            return ApiResponse<TenantResponseDto>.Ok(MapToDto(tenant));
        }

        public async Task<ApiResponse<IEnumerable<TenantResponseDto>>> GetAllAsync()
        {
            var tenants = await _unitOfWork.Tenants.GetAllAsync();
            var result = tenants.Select(MapToDto);
            return ApiResponse<IEnumerable<TenantResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<TenantResponseDto>> UpdateAsync(Guid id, UpdateTenantDto dto)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
            if (tenant == null)
                return ApiResponse<TenantResponseDto>.Fail("Tenant not found");

            tenant.Name = dto.Name;
            tenant.Phone = dto.Phone;
            tenant.Logo = dto.Logo;
            tenant.Domain = dto.Domain;
            tenant.SubscriptionEndDate = dto.SubscriptionEndDate;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<TenantResponseDto>.Ok(MapToDto(tenant), "Tenant updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
            if (tenant == null)
                return ApiResponse<bool>.Fail("Tenant not found");

            await _unitOfWork.Tenants.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Tenant deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
            if (tenant == null)
                return ApiResponse<bool>.Fail("Tenant not found");

            tenant.IsActive = !tenant.IsActive;
            tenant.Status = tenant.IsActive ? TenantStatus.Active : TenantStatus.Suspended;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            var message = tenant.IsActive ? "Tenant activated" : "Tenant suspended";
            return ApiResponse<bool>.Ok(true, message);
        }

        private static TenantResponseDto MapToDto(Tenant tenant) => new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Email = tenant.Email,
            Phone = tenant.Phone,
            Logo = tenant.Logo,
            Domain = tenant.Domain,
            IsActive = tenant.IsActive,
            Status = tenant.Status,
            SubscriptionEndDate = tenant.SubscriptionEndDate,
            CreatedAt = tenant.CreatedAt,
            UsersCount = tenant.Users?.Count ?? 0,
            VatNumber = tenant.VatNumber,   // ضيف ده
            VatRate = tenant.VatRate        // ضيف ده
        };
    }
}