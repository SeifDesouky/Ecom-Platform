// ================================================================
// EcomPlatform.Application/Services/Interfaces/IUserProfileService.cs
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Profile;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IUserProfileService
    {
        /// <summary>يجيب البروفايل الكامل لأي يوزر بالـ ID</summary>
        Task<ApiResponse<UserProfileDto>> GetProfileAsync(Guid userId);

        /// <summary>اليوزر يعدل بروفايله هو</summary>
        Task<ApiResponse<UserProfileDto>> UpdateMyProfileAsync(Guid userId, UpdateProfileDto dto);

        /// <summary>الـ Admin يعدل بروفايل أي يوزر</summary>
        Task<ApiResponse<UserProfileDto>> AdminUpdateProfileAsync(Guid targetUserId, AdminUpdateProfileDto dto);
    }
}