using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;

namespace EcomPlatform.Application.Services.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(string refreshToken);
    }
}