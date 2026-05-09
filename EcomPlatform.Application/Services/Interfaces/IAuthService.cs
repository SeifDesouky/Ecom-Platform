using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(string? ipAddress, string? deviceInfo, LoginDto dto);
        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(string plainRefreshToken, string? ipAddress, string? deviceInfo);
        Task<ApiResponse<bool>> RevokeTokenAsync(string plainRefreshToken, Guid userId);
        Task<ApiResponse<bool>> RevokeAllTokensAsync(Guid userId);
        Task<ApiResponse<bool>> RevokeTokenByIdAsync(Guid tokenId, Guid userId);
        Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId);
    }
}