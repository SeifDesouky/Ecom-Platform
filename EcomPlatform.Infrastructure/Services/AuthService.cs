// ================================================================
// EcomPlatform.Infrastructure/Services/AuthService.cs — FULL REWRITE
// ================================================================
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

        // كل user مش هيعنده أكتر من كده sessions في نفس الوقت
        private const int MaxActiveSessionsPerUser = 5;

        public AuthService(
            IUnitOfWork unitOfWork,
            JwtSettings jwtSettings,
            IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _jwtSettings = jwtSettings;
            _emailService = emailService;
        }

        // ── Register ─────────────────────────────────────────────────────

        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
        {
            var existingUsers = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                return ApiResponse<AuthResponseDto>.Fail("Email already exists");

            // منع أي حد يسجل نفسه كـ SuperAdmin
            var role = dto.Role == UserRole.SuperAdmin ? UserRole.TenantAdmin : dto.Role;

            var user = new User
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = HashPassword(dto.Password),
                Role = role,
                IsActive = true
            };

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _ = _emailService.SendWelcomeAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                "Fatora Platform");

            // Register بدون refresh token — اللي يعمل Login هيجيب token
            var accessToken = GenerateAccessToken(user);

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = string.Empty,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Registered successfully");
        }

        // ── Login ────────────────────────────────────────────────────────

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(
            LoginDto dto,
            string? ipAddress,
            string? deviceInfo)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !VerifyPassword(dto.Password, user.PasswordHash))
                return ApiResponse<AuthResponseDto>.Fail("Invalid email or password");

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("Account is disabled");

            // تنظيف الـ sessions الزيادة لو وصل للـ max
            await CleanupOldSessionsAsync(user.Id);

            // توليد refresh token جديد
            var (plainToken, tokenHash) = GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                IpAddress = ipAddress,
                DeviceInfo = deviceInfo?.Length > 512
                    ? deviceInfo[..512]
                    : deviceInfo
            };

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);

            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = plainToken,  // plain token بس للـ client، الـ hash في الـ DB
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Login successful");
        }

        // ── Refresh Token ─────────────────────────────────────────────────

        public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(
            string plainRefreshToken,
            string? ipAddress,
            string? deviceInfo)
        {
            var tokenHash = HashToken(plainRefreshToken);

            var tokens = await _unitOfWork.RefreshTokens
                .FindAsync(t => t.TokenHash == tokenHash);

            var existingToken = tokens.FirstOrDefault();

            // Token مش موجود
            if (existingToken == null)
                return ApiResponse<AuthResponseDto>.Fail("Invalid refresh token");

            // ── Reuse Detection ───────────────────────────────────────────
            // لو الـ token اتعمله revoke قبل كده ومحدش من المفروض يبعته تاني
            // ده بيدل على سرقة محتملة — نلغي كل sessions اليوزر ده
            if (existingToken.IsRevoked)
            {
                await RevokeAllTokensAsync(existingToken.UserId);
                return ApiResponse<AuthResponseDto>.Fail(
                    "Security alert: token reuse detected. All sessions have been revoked.");
            }

            // Token انتهت صلاحيته
            if (existingToken.IsExpired)
                return ApiResponse<AuthResponseDto>.Fail("Refresh token has expired");

            // جلب الـ user
            var users = await _unitOfWork.Users
                .FindAsync(u => u.Id == existingToken.UserId);
            var user = users.FirstOrDefault();

            if (user == null || !user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("User not found or disabled");

            // ── Token Rotation ────────────────────────────────────────────
            // الـ token القديم بيتشال (revoke) ويتولد token جديد
            var (newPlainToken, newTokenHash) = GenerateRefreshToken();

            // نحفظ اللي استبدله للـ audit trail
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.ReplacedByTokenHash = newTokenHash;
            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);

            // نضيف الـ token الجديد
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                IpAddress = ipAddress,
                DeviceInfo = deviceInfo?.Length > 512
                    ? deviceInfo[..512]
                    : deviceInfo
            };

            await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = newPlainToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Token refreshed");
        }

        // ── Revoke Single Token (Logout من device واحد) ───────────────────

        public async Task<ApiResponse<bool>> RevokeTokenAsync(
            string plainRefreshToken,
            Guid userId)
        {
            var tokenHash = HashToken(plainRefreshToken);

            var tokens = await _unitOfWork.RefreshTokens
                .FindAsync(t => t.TokenHash == tokenHash && t.UserId == userId);

            var token = tokens.FirstOrDefault();

            if (token == null || token.IsRevoked)
                return ApiResponse<bool>.Fail("Token not found or already revoked");

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;

            await _unitOfWork.RefreshTokens.UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Logged out successfully");
        }

        // ── Revoke All Tokens (Logout من كل الأجهزة) ─────────────────────

        public async Task<ApiResponse<bool>> RevokeAllTokensAsync(Guid userId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .FindAsync(t => t.UserId == userId && !t.IsRevoked);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.RefreshTokens.UpdateAsync(token);
            }

            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "All sessions revoked");
        }

        // ── Get Active Sessions ────────────────────────────────────────────

        public async Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .FindAsync(t =>
                    t.UserId == userId &&
                    !t.IsRevoked &&
                    t.ExpiresAt > DateTime.UtcNow);

            var sessions = activeTokens
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new ActiveSessionDto
                {
                    TokenId = t.Id,
                    DeviceInfo = t.DeviceInfo,
                    IpAddress = t.IpAddress,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    IsCurrentSession = false  // بيتحدد في الـ controller
                })
                .ToList();

            return ApiResponse<List<ActiveSessionDto>>.Ok(sessions);
        }

        // ── Private Helpers ───────────────────────────────────────────────

        private string GenerateAccessToken(User user)
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

        /// <summary>
        /// توليد refresh token:
        /// - plain: يتبعت للـ client
        /// - hash: بيتخزن في الـ DB
        /// </summary>
        private static (string plain, string hash) GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var plain = Convert.ToBase64String(randomBytes);
            var hash = HashToken(plain);
            return (plain, hash);
        }

        /// <summary>
        /// SHA-256 hash للـ token — بيتخزن في الـ DB بدل الـ plain text
        /// </summary>
        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string HashPassword(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password);

        private static bool VerifyPassword(string password, string hash) =>
            BCrypt.Net.BCrypt.Verify(password, hash);

        /// <summary>
        /// لو الـ user وصل للـ max sessions، نشيل الأقدم
        /// </summary>
        private async Task CleanupOldSessionsAsync(Guid userId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .FindAsync(t =>
                    t.UserId == userId &&
                    !t.IsRevoked &&
                    t.ExpiresAt > DateTime.UtcNow);

            var sortedTokens = activeTokens
                .OrderBy(t => t.CreatedAt)
                .ToList();

            if (sortedTokens.Count >= MaxActiveSessionsPerUser)
            {
                // نشيل الأقدم sessions علشان نعمل مكان
                var toRevoke = sortedTokens
                    .Take(sortedTokens.Count - MaxActiveSessionsPerUser + 1);

                foreach (var token in toRevoke)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    await _unitOfWork.RefreshTokens.UpdateAsync(token);
                }
            }
        }

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
