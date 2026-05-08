using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Shared.Settings;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EcomPlatform.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtSettings _jwtSettings;
        private readonly IEmailService _emailService;

        public AuthService(IUnitOfWork unitOfWork, JwtSettings jwtSettings, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _jwtSettings = jwtSettings;
            _emailService = emailService;
        }

        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
        {
            var existingUsers = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                return ApiResponse<AuthResponseDto>.Fail("Email already exists");

            // منع أي حد يسجل نفسه كـ SuperAdmin عن طريق الـ API
            var role = dto.Role == UserRole.SuperAdmin ? UserRole.TenantAdmin : dto.Role;

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = HashPassword(dto.Password),
                Role = role,
                IsActive = true,
                RefreshToken = GenerateRefreshToken(),
                RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays)
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            _ = _emailService.SendWelcomeAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                "Fatora Platform");

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = user.RefreshToken!,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Registered successfully");
        }

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !VerifyPassword(dto.Password, user.PasswordHash))
                return ApiResponse<AuthResponseDto>.Fail("Invalid email or password");

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("Account is disabled");

            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays);
            user.LastLoginAt = DateTime.UtcNow;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = user.RefreshToken!,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Login successful");
        }

        public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(string refreshToken)
        {
            var users = await _unitOfWork.Users.FindAsync(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiry > DateTime.UtcNow);

            var user = users.FirstOrDefault();

            if (user == null)
                return ApiResponse<AuthResponseDto>.Fail("Invalid or expired refresh token");

            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays);

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = token,
                RefreshToken = user.RefreshToken!,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Token refreshed");
        }

        // ── Private Helpers ──────────────────────────────────────────────────

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("role", user.Role.ToString()),
                new Claim("tenantId", user.TenantId?.ToString() ?? ""),
                new Claim("userId", user.Id.ToString()),
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private static string HashPassword(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password);

        private static bool VerifyPassword(string password, string hash) =>
            BCrypt.Net.BCrypt.Verify(password, hash);

        private static UserDto MapToUserDto(User user) => new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            TenantId = user.TenantId
        };
    }
}