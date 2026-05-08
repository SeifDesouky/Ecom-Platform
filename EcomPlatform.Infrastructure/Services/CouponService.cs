using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Coupons;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class CouponService : ICouponService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CouponService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<CouponResponseDto>> CreateAsync(CreateCouponDto dto)
        {
            var existing = await _unitOfWork.Coupons.FindAsync(c =>
                c.Code == dto.Code.ToUpper() && c.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<CouponResponseDto>.Fail("Coupon code already exists");

            var coupon = new Coupon
            {
                Code = dto.Code.ToUpper(),
                Description = dto.Description,
                Type = dto.Type,
                Value = dto.Value,
                MinOrderAmount = dto.MinOrderAmount,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                UsageLimit = dto.UsageLimit,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                TenantId = dto.TenantId,
                IsActive = true
            };

            await _unitOfWork.Coupons.AddAsync(coupon);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CouponResponseDto>.Ok(MapToDto(coupon), "Coupon created successfully");
        }

        public async Task<ApiResponse<CouponResponseDto>> GetByIdAsync(Guid id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return ApiResponse<CouponResponseDto>.Fail("Coupon not found");

            return ApiResponse<CouponResponseDto>.Ok(MapToDto(coupon));
        }

        public async Task<ApiResponse<PagedResponse<CouponResponseDto>>> GetAllByTenantAsync(Guid tenantId, PaginationParams pagination)
        {
            var all = await _unitOfWork.Coupons.FindAsync(c => c.TenantId == tenantId);
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(c => c.CreatedAt)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .Select(MapToDto)
                .ToList();
            var result = PagedResponse<CouponResponseDto>.Create(items, totalCount, pagination);
            return ApiResponse<PagedResponse<CouponResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return ApiResponse<bool>.Fail("Coupon not found");

            await _unitOfWork.Coupons.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Coupon deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var coupon = await _unitOfWork.Coupons.GetByIdAsync(id);
            if (coupon == null)
                return ApiResponse<bool>.Fail("Coupon not found");

            coupon.IsActive = !coupon.IsActive;
            await _unitOfWork.Coupons.UpdateAsync(coupon);
            await _unitOfWork.SaveChangesAsync();

            var message = coupon.IsActive ? "Coupon activated" : "Coupon deactivated";
            return ApiResponse<bool>.Ok(true, message);
        }

        public async Task<ApiResponse<CouponValidationResponseDto>> ValidateAsync(ValidateCouponDto dto)
        {
            var coupons = await _unitOfWork.Coupons.FindAsync(c =>
                c.Code == dto.Code.ToUpper() && c.TenantId == dto.TenantId);
            var coupon = coupons.FirstOrDefault();

            if (coupon == null)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = "Coupon not found"
                });

            if (!coupon.IsActive)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = "Coupon is not active"
                });

            if (coupon.StartDate.HasValue && DateTime.UtcNow < coupon.StartDate)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = "Coupon has not started yet"
                });

            if (coupon.EndDate.HasValue && DateTime.UtcNow > coupon.EndDate)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = "Coupon has expired"
                });

            if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = "Coupon usage limit reached"
                });

            if (coupon.MinOrderAmount.HasValue && dto.OrderAmount < coupon.MinOrderAmount)
                return ApiResponse<CouponValidationResponseDto>.Ok(new()
                {
                    IsValid = false,
                    Message = $"Minimum order amount is {coupon.MinOrderAmount}"
                });

            decimal discountAmount = coupon.Type == CouponType.Percentage
                ? dto.OrderAmount * coupon.Value / 100
                : coupon.Value;

            if (coupon.MaxDiscountAmount.HasValue && discountAmount > coupon.MaxDiscountAmount)
                discountAmount = coupon.MaxDiscountAmount.Value;

            return ApiResponse<CouponValidationResponseDto>.Ok(new()
            {
                IsValid = true,
                Message = "Coupon is valid",
                DiscountAmount = discountAmount,
                Coupon = MapToDto(coupon)
            });
        }

        private static CouponResponseDto MapToDto(Coupon coupon) => new()
        {
            Id = coupon.Id,
            Code = coupon.Code,
            Description = coupon.Description,
            Type = coupon.Type,
            Value = coupon.Value,
            MinOrderAmount = coupon.MinOrderAmount,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            UsageLimit = coupon.UsageLimit,
            UsageCount = coupon.UsageCount,
            IsActive = coupon.IsActive,
            StartDate = coupon.StartDate,
            EndDate = coupon.EndDate,
            TenantId = coupon.TenantId,
            CreatedAt = coupon.CreatedAt
        };
    }
}