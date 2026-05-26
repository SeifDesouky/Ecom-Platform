// ================================================================
// EcomPlatform.Infrastructure/Services/UserProfileService.cs
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Profile;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserProfileService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ── GET Profile ───────────────────────────────────────────────────
        public async Task<ApiResponse<UserProfileDto>> GetProfileAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<UserProfileDto>.Fail("User not found");

            // جيب الـ Profile لو موجود
            var profiles = await _unitOfWork.UserProfiles.FindAsync(p => p.UserId == userId);
            var profile = profiles.FirstOrDefault();

            return ApiResponse<UserProfileDto>.Ok(MapToDto(user, profile));
        }

        // ── UPDATE (self) ─────────────────────────────────────────────────
        public async Task<ApiResponse<UserProfileDto>> UpdateMyProfileAsync(Guid userId, UpdateProfileDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return ApiResponse<UserProfileDto>.Fail("User not found");

            // تحديث بيانات الـ User الأساسية
            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.Phone != null) user.Phone = dto.Phone;

            await _unitOfWork.Users.UpdateAsync(user);

            // جيب أو أنشئ الـ UserProfile
            var profiles = await _unitOfWork.UserProfiles.FindAsync(p => p.UserId == userId);
            var profile = profiles.FirstOrDefault();

            if (profile == null)
            {
                profile = new UserProfile { UserId = userId };
                ApplyProfileUpdates(profile, dto);
                await _unitOfWork.UserProfiles.AddAsync(profile);
            }
            else
            {
                ApplyProfileUpdates(profile, dto);
                await _unitOfWork.UserProfiles.UpdateAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<UserProfileDto>.Ok(MapToDto(user, profile), "Profile updated successfully");
        }

        // ── ADMIN UPDATE ──────────────────────────────────────────────────
        public async Task<ApiResponse<UserProfileDto>> AdminUpdateProfileAsync(
            Guid targetUserId,
            AdminUpdateProfileDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(targetUserId);
            if (user == null)
                return ApiResponse<UserProfileDto>.Fail("User not found");

            // Admin-only fields
            if (dto.Email != null) user.Email = dto.Email;
            if (dto.Role != null) user.Role = dto.Role.Value;
            if (dto.IsActive != null) user.IsActive = dto.IsActive.Value;

            // الـ fields المشتركة
            if (dto.FirstName != null) user.FirstName = dto.FirstName;
            if (dto.LastName != null) user.LastName = dto.LastName;
            if (dto.Phone != null) user.Phone = dto.Phone;

            await _unitOfWork.Users.UpdateAsync(user);

            var profiles = await _unitOfWork.UserProfiles.FindAsync(p => p.UserId == targetUserId);
            var profile = profiles.FirstOrDefault();

            if (profile == null)
            {
                profile = new UserProfile { UserId = targetUserId };
                ApplyProfileUpdates(profile, dto);
                await _unitOfWork.UserProfiles.AddAsync(profile);
            }
            else
            {
                ApplyProfileUpdates(profile, dto);
                await _unitOfWork.UserProfiles.UpdateAsync(profile);
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<UserProfileDto>.Ok(MapToDto(user, profile), "Profile updated by admin");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>تطبق أي field غير null من الـ DTO على الـ UserProfile</summary>
        private static void ApplyProfileUpdates(UserProfile profile, UpdateProfileDto dto)
        {
            if (dto.AvatarUrl != null) profile.AvatarUrl = dto.AvatarUrl;
            if (dto.Bio != null) profile.Bio = dto.Bio;
            if (dto.DateOfBirth != null) profile.DateOfBirth = dto.DateOfBirth;
            if (dto.AddressLine1 != null) profile.AddressLine1 = dto.AddressLine1;
            if (dto.AddressLine2 != null) profile.AddressLine2 = dto.AddressLine2;
            if (dto.City != null) profile.City = dto.City;
            if (dto.State != null) profile.State = dto.State;
            if (dto.Country != null) profile.Country = dto.Country;
            if (dto.PostalCode != null) profile.PostalCode = dto.PostalCode;
        }

        private static UserProfileDto MapToDto(User user, UserProfile? profile) => new()
        {
            // User fields
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            LastLoginAt = user.LastLoginAt,

            // Profile fields (null-safe)
            AvatarUrl = profile?.AvatarUrl,
            Bio = profile?.Bio,
            DateOfBirth = profile?.DateOfBirth,
            AddressLine1 = profile?.AddressLine1,
            AddressLine2 = profile?.AddressLine2,
            City = profile?.City,
            State = profile?.State,
            Country = profile?.Country,
            PostalCode = profile?.PostalCode,
        };
    }
}