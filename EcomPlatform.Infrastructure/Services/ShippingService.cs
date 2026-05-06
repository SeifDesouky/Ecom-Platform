using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Shipping;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class ShippingService : IShippingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShippingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<ShippingZoneResponseDto>> CreateZoneAsync(CreateShippingZoneDto dto)
        {
            var zone = new ShippingZone
            {
                Name = dto.Name,
                Description = dto.Description,
                TenantId = dto.TenantId,
                IsActive = true
            };

            await _unitOfWork.ShippingZones.AddAsync(zone);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ShippingZoneResponseDto>.Ok(MapZoneToDto(zone), "Shipping zone created successfully");
        }

        public async Task<ApiResponse<IEnumerable<ShippingZoneResponseDto>>> GetZonesByTenantAsync(Guid tenantId)
        {
            var zones = await _unitOfWork.ShippingZones.FindAsync(z => z.TenantId == tenantId);
            var zonesList = zones.ToList();

            foreach (var zone in zonesList)
            {
                var methods = await _unitOfWork.ShippingMethods.FindAsync(m => m.ShippingZoneId == zone.Id);
                zone.Methods = methods.ToList();
            }

            return ApiResponse<IEnumerable<ShippingZoneResponseDto>>.Ok(zonesList.Select(MapZoneToDto));
        }

        public async Task<ApiResponse<ShippingZoneResponseDto>> GetZoneByIdAsync(Guid id)
        {
            var zone = await _unitOfWork.ShippingZones.GetByIdAsync(id);
            if (zone == null)
                return ApiResponse<ShippingZoneResponseDto>.Fail("Shipping zone not found");

            var methods = await _unitOfWork.ShippingMethods.FindAsync(m => m.ShippingZoneId == id);
            zone.Methods = methods.ToList();

            return ApiResponse<ShippingZoneResponseDto>.Ok(MapZoneToDto(zone));
        }

        public async Task<ApiResponse<bool>> DeleteZoneAsync(Guid id)
        {
            var zone = await _unitOfWork.ShippingZones.GetByIdAsync(id);
            if (zone == null)
                return ApiResponse<bool>.Fail("Shipping zone not found");

            await _unitOfWork.ShippingZones.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Shipping zone deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleZoneStatusAsync(Guid id)
        {
            var zone = await _unitOfWork.ShippingZones.GetByIdAsync(id);
            if (zone == null)
                return ApiResponse<bool>.Fail("Shipping zone not found");

            zone.IsActive = !zone.IsActive;
            await _unitOfWork.ShippingZones.UpdateAsync(zone);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, zone.IsActive ? "Zone activated" : "Zone deactivated");
        }

        public async Task<ApiResponse<ShippingMethodResponseDto>> CreateMethodAsync(CreateShippingMethodDto dto)
        {
            var zone = await _unitOfWork.ShippingZones.GetByIdAsync(dto.ShippingZoneId);
            if (zone == null)
                return ApiResponse<ShippingMethodResponseDto>.Fail("Shipping zone not found");

            var method = new ShippingMethod
            {
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                Cost = dto.Cost,
                MinOrderAmount = dto.MinOrderAmount,
                MaxOrderAmount = dto.MaxOrderAmount,
                EstimatedDaysMin = dto.EstimatedDaysMin,
                EstimatedDaysMax = dto.EstimatedDaysMax,
                ShippingZoneId = dto.ShippingZoneId,
                IsActive = true
            };

            await _unitOfWork.ShippingMethods.AddAsync(method);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<ShippingMethodResponseDto>.Ok(MapMethodToDto(method), "Shipping method created successfully");
        }

        public async Task<ApiResponse<bool>> DeleteMethodAsync(Guid id)
        {
            var method = await _unitOfWork.ShippingMethods.GetByIdAsync(id);
            if (method == null)
                return ApiResponse<bool>.Fail("Shipping method not found");

            await _unitOfWork.ShippingMethods.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Shipping method deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleMethodStatusAsync(Guid id)
        {
            var method = await _unitOfWork.ShippingMethods.GetByIdAsync(id);
            if (method == null)
                return ApiResponse<bool>.Fail("Shipping method not found");

            method.IsActive = !method.IsActive;
            await _unitOfWork.ShippingMethods.UpdateAsync(method);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, method.IsActive ? "Method activated" : "Method deactivated");
        }

        public async Task<ApiResponse<IEnumerable<ShippingMethodResponseDto>>> CalculateShippingAsync(CalculateShippingDto dto)
        {
            var zones = await _unitOfWork.ShippingZones.FindAsync(z =>
                z.TenantId == dto.TenantId && z.IsActive);

            var availableMethods = new List<ShippingMethod>();

            foreach (var zone in zones)
            {
                var methods = await _unitOfWork.ShippingMethods.FindAsync(m =>
                    m.ShippingZoneId == zone.Id && m.IsActive);

                foreach (var method in methods)
                {
                    if (method.Type == ShippingType.Free)
                    {
                        availableMethods.Add(method);
                    }
                    else if (method.MinOrderAmount.HasValue && dto.OrderAmount < method.MinOrderAmount)
                    {
                        continue;
                    }
                    else if (method.MaxOrderAmount.HasValue && dto.OrderAmount > method.MaxOrderAmount)
                    {
                        continue;
                    }
                    else
                    {
                        availableMethods.Add(method);
                    }
                }
            }

            return ApiResponse<IEnumerable<ShippingMethodResponseDto>>.Ok(
                availableMethods.Select(MapMethodToDto),
                "Available shipping methods");
        }

        private static ShippingZoneResponseDto MapZoneToDto(ShippingZone zone) => new()
        {
            Id = zone.Id,
            Name = zone.Name,
            Description = zone.Description,
            IsActive = zone.IsActive,
            TenantId = zone.TenantId,
            CreatedAt = zone.CreatedAt,
            Methods = zone.Methods?.Select(MapMethodToDto).ToList() ?? new()
        };

        private static ShippingMethodResponseDto MapMethodToDto(ShippingMethod method) => new()
        {
            Id = method.Id,
            Name = method.Name,
            Description = method.Description,
            Type = method.Type,
            Cost = method.Cost,
            MinOrderAmount = method.MinOrderAmount,
            MaxOrderAmount = method.MaxOrderAmount,
            EstimatedDaysMin = method.EstimatedDaysMin,
            EstimatedDaysMax = method.EstimatedDaysMax,
            IsActive = method.IsActive,
            ShippingZoneId = method.ShippingZoneId,
            CreatedAt = method.CreatedAt
        };
    }
}