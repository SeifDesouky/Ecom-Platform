// ================================================================
// EcomPlatform.Application/Services/Interfaces/IAuthService.cs
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAuthService
    {
        // ── Standard Auth ─────────────────────────────────────────────────
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);

        Task<ApiResponse<AuthResponseDto>> LoginAsync(
            string? ipAddress,
            string? deviceInfo,
            LoginDto dto);

        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(
            string plainRefreshToken,
            string? ipAddress,
            string? deviceInfo);

        Task<ApiResponse<bool>> RevokeTokenAsync(string plainRefreshToken, Guid userId);
        Task<ApiResponse<bool>> RevokeAllTokensAsync(Guid userId);
        Task<ApiResponse<bool>> RevokeTokenByIdAsync(Guid tokenId, Guid userId);
        Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId);
        Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto);
        Task<ApiResponse<bool>> VerifyEmailAsync(VerifyEmailDto dto);
        // في نهاية الـ interface — بعد LoginWithAppleAsync
        Task<ApiResponse<AuthResponseDto>> OnboardStoreAsync(Guid userId, OnboardStoreDto dto);


        // ✅ جديد: Social Login
        Task<ApiResponse<AuthResponseDto>> LoginWithGoogleAsync(
            string? ipAddress,
            string? deviceInfo,
            GoogleLoginDto dto);

        Task<ApiResponse<AuthResponseDto>> LoginWithAppleAsync(
            string? ipAddress,
            string? deviceInfo,
            AppleLoginDto dto);
    }
}