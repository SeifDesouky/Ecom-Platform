using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Customers;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<CustomerResponseDto>> CreateAsync(CreateCustomerDto dto)
        {
            var existing = await _unitOfWork.Customers.FindAsync(c =>
                c.Email == dto.Email && c.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<CustomerResponseDto>.Fail("Email already exists");

            var customer = new Customer
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                Avatar = dto.Avatar,
                BirthDate = dto.BirthDate,
                Notes = dto.Notes,
                TenantId = dto.TenantId,
                IsActive = true
            };

            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CustomerResponseDto>.Ok(MapToDto(customer), "Customer created successfully");
        }

        public async Task<ApiResponse<CustomerResponseDto>> GetByIdAsync(Guid id)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null)
                return ApiResponse<CustomerResponseDto>.Fail("Customer not found");

            return ApiResponse<CustomerResponseDto>.Ok(MapToDto(customer));
        }

        public async Task<ApiResponse<IEnumerable<CustomerResponseDto>>> GetAllByTenantAsync(Guid tenantId)
        {
            var customers = await _unitOfWork.Customers.FindAsync(c => c.TenantId == tenantId);
            var result = customers.Select(MapToDto);
            return ApiResponse<IEnumerable<CustomerResponseDto>>.Ok(result);
        }

        public async Task<ApiResponse<CustomerResponseDto>> UpdateAsync(Guid id, UpdateCustomerDto dto)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null)
                return ApiResponse<CustomerResponseDto>.Fail("Customer not found");

            customer.FirstName = dto.FirstName;
            customer.LastName = dto.LastName;
            customer.Phone = dto.Phone;
            customer.Avatar = dto.Avatar;
            customer.BirthDate = dto.BirthDate;
            customer.Notes = dto.Notes;

            await _unitOfWork.Customers.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CustomerResponseDto>.Ok(MapToDto(customer), "Customer updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null)
                return ApiResponse<bool>.Fail("Customer not found");

            await _unitOfWork.Customers.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Customer deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(id);
            if (customer == null)
                return ApiResponse<bool>.Fail("Customer not found");

            customer.IsActive = !customer.IsActive;

            await _unitOfWork.Customers.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            var message = customer.IsActive ? "Customer activated" : "Customer deactivated";
            return ApiResponse<bool>.Ok(true, message);
        }

        public async Task<ApiResponse<CustomerAddressResponseDto>> AddAddressAsync(CreateCustomerAddressDto dto)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(dto.CustomerId);
            if (customer == null)
                return ApiResponse<CustomerAddressResponseDto>.Fail("Customer not found");

            if (dto.IsDefault)
            {
                var existingAddresses = await _unitOfWork.CustomerAddresses
                    .FindAsync(a => a.CustomerId == dto.CustomerId);
                foreach (var addr in existingAddresses)
                {
                    addr.IsDefault = false;
                    await _unitOfWork.CustomerAddresses.UpdateAsync(addr);
                }
            }

            var address = new CustomerAddress
            {
                Title = dto.Title,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Address = dto.Address,
                City = dto.City,
                Country = dto.Country,
                PostalCode = dto.PostalCode,
                IsDefault = dto.IsDefault,
                CustomerId = dto.CustomerId
            };

            await _unitOfWork.CustomerAddresses.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CustomerAddressResponseDto>.Ok(MapAddressToDto(address), "Address added successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAddressAsync(Guid addressId)
        {
            var address = await _unitOfWork.CustomerAddresses.GetByIdAsync(addressId);
            if (address == null)
                return ApiResponse<bool>.Fail("Address not found");

            await _unitOfWork.CustomerAddresses.DeleteAsync(addressId);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Address deleted successfully");
        }

        public async Task<ApiResponse<bool>> SetDefaultAddressAsync(Guid addressId)
        {
            var address = await _unitOfWork.CustomerAddresses.GetByIdAsync(addressId);
            if (address == null)
                return ApiResponse<bool>.Fail("Address not found");

            var allAddresses = await _unitOfWork.CustomerAddresses
                .FindAsync(a => a.CustomerId == address.CustomerId);

            foreach (var addr in allAddresses)
            {
                addr.IsDefault = addr.Id == addressId;
                await _unitOfWork.CustomerAddresses.UpdateAsync(addr);
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Default address updated successfully");
        }

        private static CustomerResponseDto MapToDto(Customer customer) => new()
        {
            Id = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.Email,
            Phone = customer.Phone,
            Avatar = customer.Avatar,
            BirthDate = customer.BirthDate,
            IsActive = customer.IsActive,
            IsEmailVerified = customer.IsEmailVerified,
            Notes = customer.Notes,
            TotalSpent = customer.TotalSpent,
            TotalOrders = customer.TotalOrders,
            TenantId = customer.TenantId,
            CreatedAt = customer.CreatedAt,
            Addresses = customer.Addresses?.Select(MapAddressToDto).ToList() ?? new()
        };

        private static CustomerAddressResponseDto MapAddressToDto(CustomerAddress address) => new()
        {
            Id = address.Id,
            Title = address.Title,
            FullName = address.FullName,
            Phone = address.Phone,
            Address = address.Address,
            City = address.City,
            Country = address.Country,
            PostalCode = address.PostalCode,
            IsDefault = address.IsDefault
        };
    }
}