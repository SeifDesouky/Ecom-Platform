using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Users;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<UserResponseDto>> CreateAsync(CreateUserDto dto)
        {
            var existing = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            if (existing.Any())
                return ApiResponse<UserResponseDto>.Fail("Email already exists");

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                TenantId = dto.TenantId,
                IsActive = true
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // ← إنشاء UserProfile للـ user الجديد
            var profile = new UserProfile { UserId = user.Id };
            await _unitOfWork.UserProfiles.AddAsync(profile);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<UserResponseDto>.Ok(MapToDto(user), "User created successfully");
        }

        public async Task<ApiResponse<UserResponseDto>> GetByIdAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<UserResponseDto>.Fail("User not found");

            return ApiResponse<UserResponseDto>.Ok(MapToDto(user));
        }

        public async Task<ApiResponse<IEnumerable<UserResponseDto>>> GetAllByTenantAsync(Guid tenantId)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.TenantId == tenantId);
            return ApiResponse<IEnumerable<UserResponseDto>>.Ok(users.Select(MapToDto));
        }

        public async Task<ApiResponse<UserResponseDto>> UpdateAsync(Guid id, UpdateUserDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<UserResponseDto>.Fail("User not found");

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Phone = dto.Phone;
            user.Role = dto.Role;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<UserResponseDto>.Ok(MapToDto(user), "User updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<bool>.Fail("User not found");

            await _unitOfWork.Users.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "User deleted successfully");
        }

        public async Task<ApiResponse<bool>> ToggleStatusAsync(Guid id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<bool>.Fail("User not found");

            user.IsActive = !user.IsActive;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, user.IsActive ? "User activated" : "User deactivated");
        }

        public async Task<ApiResponse<bool>> ChangeRoleAsync(Guid id, UserRole role)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<bool>.Fail("User not found");

            user.Role = role;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Role updated successfully");
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(Guid id, string newPassword)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
                return ApiResponse<bool>.Fail("User not found");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Password reset successfully");
        }

        private static UserResponseDto MapToDto(User user) => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            LastLoginAt = user.LastLoginAt,
            TenantId = user.TenantId,
            CreatedAt = user.CreatedAt
        };
    }
}