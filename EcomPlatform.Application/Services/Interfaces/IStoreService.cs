// ================================================================
// EcomPlatform.Application/Services/Interfaces/IStoreService.cs
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.DTOs.Store;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IStoreService
    {
        /// <summary>
        /// إنشاء متجر جديد + TenantAdmin في transaction واحدة — anonymous
        /// </summary>
        Task<ApiResponse<AuthResponseDto>> RegisterStoreAsync(RegisterStoreDto dto);

        /// <summary>
        /// فحص إذا كان الـ slug متاح أم لا
        /// </summary>
        Task<SlugAvailabilityResponseDto> CheckSlugAvailabilityAsync(string slug);
    }
}