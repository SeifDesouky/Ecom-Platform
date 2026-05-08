// ================================================================
// EcomPlatform.Application/Services/Interfaces/IAuthService.cs — UPDATED
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto, string? ipAddress, string? deviceInfo);
        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(string refreshToken, string? ipAddress, string? deviceInfo);

        // Logout من الـ device الحالي (revoke token واحد)
        Task<ApiResponse<bool>> RevokeTokenAsync(string refreshToken, Guid userId);

        // Logout من كل الأجهزة (revoke all tokens للـ user)
        Task<ApiResponse<bool>> RevokeAllTokensAsync(Guid userId);

        // عرض كل الـ active sessions للـ user
        Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId);
    }
}
