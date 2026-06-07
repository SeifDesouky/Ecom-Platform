using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Shared.Settings;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
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
        private readonly IAuditLogService _auditLogService;
        private readonly GoogleAuthSettings _googleSettings;
        private readonly AppleAuthSettings _appleSettings;

        private const int MaxActiveSessionsPerUser = 5;

        private sealed record AppleTokenPayload(string Subject, string Email);

        public AuthService(
            IUnitOfWork unitOfWork,
            JwtSettings jwtSettings,
            IEmailService emailService,
            IAuditLogService auditLogService,
            IOptions<GoogleAuthSettings> googleSettings,
            IOptions<AppleAuthSettings> appleSettings)
        {
            _unitOfWork = unitOfWork;
            _jwtSettings = jwtSettings;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _googleSettings = googleSettings.Value;
            _appleSettings = appleSettings.Value;
        }

        // ── Google Login ──────────────────────────────────────────────────────
        public async Task<ApiResponse<AuthResponseDto>> LoginWithGoogleAsync(
            string? ipAddress,
            string? deviceInfo,
            GoogleLoginDto dto)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var validationSettings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_googleSettings.ClientId]
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, validationSettings);
            }
            catch (InvalidJwtException ex)
            {
                return ApiResponse<AuthResponseDto>.Fail($"Invalid Google token: {ex.Message}");
            }

            var user = await FindOrCreateSocialUserAsync(
                googleId: payload.Subject,
                appleId: null,
                email: payload.Email,
                firstName: payload.GivenName ?? "User",
                lastName: payload.FamilyName ?? "",
                isEmailVerified: payload.EmailVerified);

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("Account is disabled");

            return await CreateAuthSessionAsync(user, ipAddress, deviceInfo, "Google");
        }

        // ── Apple Login ───────────────────────────────────────────────────────
        public async Task<ApiResponse<AuthResponseDto>> LoginWithAppleAsync(
            string? ipAddress,
            string? deviceInfo,
            AppleLoginDto dto)
        {
            AppleTokenPayload payload;
            try
            {
                payload = await VerifyAppleTokenAsync(dto.IdToken);
            }
            catch (Exception ex)
            {
                return ApiResponse<AuthResponseDto>.Fail($"Invalid Apple token: {ex.Message}");
            }

            var firstName = dto.FirstName ?? "Apple";
            var lastName = dto.LastName ?? "User";

            var user = await FindOrCreateSocialUserAsync(
                googleId: null,
                appleId: payload.Subject,
                email: payload.Email,
                firstName: firstName,
                lastName: lastName,
                isEmailVerified: true);

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("Account is disabled");

            if (dto.FirstName != null && user.FirstName == "Apple")
            {
                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName ?? "";
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
            }

            return await CreateAuthSessionAsync(user, ipAddress, deviceInfo, "Apple");
        }

        // ── Shared: Find or Create Social User ────────────────────────────────
        private async Task<User> FindOrCreateSocialUserAsync(
            string? googleId,
            string? appleId,
            string email,
            string firstName,
            string lastName,
            bool isEmailVerified)
        {
            IEnumerable<User> found;

            if (googleId != null)
            {
                found = await _unitOfWork.Users.FindAsync(u => u.GoogleId == googleId);
                if (found.FirstOrDefault() is User byGoogleId)
                    return byGoogleId;
            }

            if (appleId != null)
            {
                found = await _unitOfWork.Users.FindAsync(u => u.AppleId == appleId);
                if (found.FirstOrDefault() is User byAppleId)
                    return byAppleId;
            }

            found = await _unitOfWork.Users.FindAsync(u => u.Email == email);
            var existingUser = found.FirstOrDefault();

            if (existingUser != null)
            {
                if (googleId != null && existingUser.GoogleId == null)
                    existingUser.GoogleId = googleId;

                if (appleId != null && existingUser.AppleId == null)
                    existingUser.AppleId = appleId;

                if (!existingUser.IsEmailVerified && isEmailVerified)
                    existingUser.IsEmailVerified = true;

                await _unitOfWork.Users.UpdateAsync(existingUser);
                await _unitOfWork.SaveChangesAsync();

                return existingUser;
            }

            var newUser = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = string.Empty,
                PasswordHash = null,
                Role = UserRole.TenantAdmin,
                IsActive = true,
                IsEmailVerified = isEmailVerified,
                GoogleId = googleId,
                AppleId = appleId,
            };

            await _unitOfWork.Users.AddAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            var profile = new UserProfile { UserId = newUser.Id };
            await _unitOfWork.UserProfiles.AddAsync(profile);
            await _unitOfWork.SaveChangesAsync();

            _ = _emailService.SendWelcomeAsync(
                newUser.Email,
                $"{newUser.FirstName} {newUser.LastName}".Trim(),
                "Fatora Platform");

            return newUser;
        }

        // ── Shared: Create Auth Session ───────────────────────────────────────
        private async Task<ApiResponse<AuthResponseDto>> CreateAuthSessionAsync(
            User user,
            string? ipAddress,
            string? deviceInfo,
            string loginProvider)
        {
            await CleanupOldSessionsAsync(user.Id);

            var (plainToken, tokenHash) = GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                IpAddress = ipAddress,
                DeviceInfo = deviceInfo?.Length > 512 ? deviceInfo[..512] : deviceInfo
            };

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);

            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);

            await _auditLogService.LogAsync(
                entityName: "Auth",
                entityId: user.Id.ToString(),
                action: AuditAction.Login,
                userId: user.Id,
                tenantId: user.TenantId,
                newValue: $"{loginProvider} login from IP: {ipAddress}");

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = plainToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, $"{loginProvider} login successful");
        }

        // ── Apple Token Verification ──────────────────────────────────────────
        private async Task<AppleTokenPayload> VerifyAppleTokenAsync(string idToken)
        {
            using var httpClient = new HttpClient();
            var keysJson = await httpClient.GetStringAsync("https://appleid.apple.com/auth/keys");
            var jwks = new JsonWebKeySet(keysJson);

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(idToken);
            var kid = jwtToken.Header.Kid;

            var matchingKey = jwks.Keys.FirstOrDefault(k => k.Kid == kid)
                ?? throw new SecurityTokenException("No matching Apple public key found");

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://appleid.apple.com",
                ValidateAudience = true,
                ValidAudience = _appleSettings.ClientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = matchingKey,
            };

            var principal = handler.ValidateToken(idToken, validationParams, out _);

            return new AppleTokenPayload(
                Subject: principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? throw new SecurityTokenException("Missing sub claim"),
                Email: principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                    ?? throw new SecurityTokenException("Missing email claim")
            );
        }

        // ── Register ──────────────────────────────────────────────────────────
        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto)
        {
            var existingUsers = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                return ApiResponse<AuthResponseDto>.Fail("Email already exists");

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

            var profile = new UserProfile { UserId = user.Id };
            await _unitOfWork.UserProfiles.AddAsync(profile);
            await _unitOfWork.SaveChangesAsync();

            _ = _emailService.SendWelcomeAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                "Fatora Platform");

            var (plainToken, tokenHash) = GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
            };

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);

            await _auditLogService.LogAsync(
                entityName: "Auth",
                entityId: user.Id.ToString(),
                action: AuditAction.Create,
                userId: user.Id,
                tenantId: user.TenantId,
                newValue: $"User '{user.Email}' registered with role {role}");

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = plainToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Registered successfully");
        }

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(
            string? ipAddress,
            string? deviceInfo,
            LoginDto dto)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || user.PasswordHash == null || !VerifyPassword(dto.Password, user.PasswordHash))
            {
                if (user != null)
                    await _auditLogService.LogAsync(
                        entityName: "Auth",
                        entityId: user.Id.ToString(),
                        action: AuditAction.FailedLogin,
                        userId: user.Id,
                        tenantId: user.TenantId,
                        newValue: $"Failed login attempt from IP: {ipAddress}");

                return ApiResponse<AuthResponseDto>.Fail("Invalid email or password");
            }

            if (!user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("Account is disabled");

            await CleanupOldSessionsAsync(user.Id);

            var (plainToken, tokenHash) = GenerateRefreshToken();

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                IpAddress = ipAddress,
                DeviceInfo = deviceInfo?.Length > 512 ? deviceInfo[..512] : deviceInfo
            };

            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);

            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var accessToken = GenerateAccessToken(user);

            await _auditLogService.LogAsync(
                entityName: "Auth",
                entityId: user.Id.ToString(),
                action: AuditAction.Login,
                userId: user.Id,
                tenantId: user.TenantId,
                newValue: $"Login from IP: {ipAddress}");

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = accessToken,
                RefreshToken = plainToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Login successful");
        }

        public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(
            string plainRefreshToken,
            string? ipAddress,
            string? deviceInfo)
        {
            var tokenHash = HashToken(plainRefreshToken);
            var tokens = await _unitOfWork.RefreshTokens.FindAsync(t => t.TokenHash == tokenHash);
            var existingToken = tokens.FirstOrDefault();

            if (existingToken == null)
                return ApiResponse<AuthResponseDto>.Fail("Invalid refresh token");

            var users = await _unitOfWork.Users.FindAsync(u => u.Id == existingToken.UserId);
            var user = users.FirstOrDefault();

            if (existingToken.IsRevoked)
            {
                await RevokeAllTokensExceptAsync(existingToken.UserId, existingToken.Id);
                await _auditLogService.LogAsync(
                    entityName: "Auth",
                    entityId: existingToken.UserId.ToString(),
                    action: AuditAction.SecurityAlert,
                    userId: existingToken.UserId,
                    tenantId: user?.TenantId,
                    newValue: $"Token reuse detected from IP: {ipAddress}");

                return ApiResponse<AuthResponseDto>.Fail("Security alert: token reuse detected.");
            }

            if (existingToken.IsExpired)
                return ApiResponse<AuthResponseDto>.Fail("Refresh token has expired");

            if (user == null || !user.IsActive)
                return ApiResponse<AuthResponseDto>.Fail("User not found or disabled");

            var (newPlainToken, newTokenHash) = GenerateRefreshToken();

            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.ReplacedByTokenHash = newTokenHash;
            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);

            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newTokenHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                IpAddress = ipAddress,
                DeviceInfo = deviceInfo?.Length > 512 ? deviceInfo[..512] : deviceInfo
            };

            await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = GenerateAccessToken(user),
                RefreshToken = newPlainToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                User = MapToUserDto(user)
            }, "Token refreshed");
        }

        public async Task<ApiResponse<bool>> RevokeTokenAsync(string plainRefreshToken, Guid userId)
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

            await _auditLogService.LogAsync("Auth", userId.ToString(), AuditAction.Logout, userId, null,
                $"Session revoked for token ID: {token.Id}");

            return ApiResponse<bool>.Ok(true, "Logged out successfully");
        }

        public async Task<ApiResponse<bool>> RevokeAllTokensAsync(Guid userId)
        {
            await RevokeAllTokensExceptAsync(userId, excludeTokenId: null);
            await _auditLogService.LogAsync("Auth", userId.ToString(), AuditAction.Logout, userId, null, "All sessions revoked");
            return ApiResponse<bool>.Ok(true, "All sessions revoked");
        }

        public async Task<ApiResponse<bool>> RevokeTokenByIdAsync(Guid tokenId, Guid userId)
        {
            var token = await _unitOfWork.RefreshTokens.GetByIdAsync(tokenId);

            if (token == null || token.UserId != userId)
                return ApiResponse<bool>.Fail("Session not found");

            if (token.IsRevoked)
                return ApiResponse<bool>.Fail("Session already revoked");

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await _unitOfWork.RefreshTokens.UpdateAsync(token);
            await _unitOfWork.SaveChangesAsync();

            await _auditLogService.LogAsync("Auth", userId.ToString(), AuditAction.Logout, userId, null,
                $"Session {tokenId} revoked manually");

            return ApiResponse<bool>.Ok(true, "Session revoked successfully");
        }

        public async Task<ApiResponse<List<ActiveSessionDto>>> GetActiveSessionsAsync(Guid userId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .FindAsync(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

            var sessions = activeTokens
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new ActiveSessionDto
                {
                    TokenId = t.Id,
                    DeviceInfo = t.DeviceInfo,
                    IpAddress = t.IpAddress,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    IsCurrentSession = false
                }).ToList();

            return ApiResponse<List<ActiveSessionDto>>.Ok(sessions);
        }

        public async Task<ApiResponse<bool>> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var users = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null)
                return ApiResponse<bool>.Ok(true, "If this email exists, a reset link has been sent");

            var oldTokens = await _unitOfWork.PasswordResetTokens.FindAsync(t => t.UserId == user.Id && !t.IsUsed);
            foreach (var old in oldTokens)
            {
                old.IsUsed = true;
                await _unitOfWork.PasswordResetTokens.UpdateAsync(old);
            }

            var plainToken = Guid.NewGuid().ToString("N");
            var resetToken = new PasswordResetToken
            {
                UserId = user.Id,
                Token = plainToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                IsUsed = false
            };

            await _unitOfWork.PasswordResetTokens.AddAsync(resetToken);
            await _unitOfWork.SaveChangesAsync();

            var resetLink = $"https://yourapp.com/reset-password?token={plainToken}";
            _ = _emailService.SendPasswordResetAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), resetLink);

            return ApiResponse<bool>.Ok(true, "If this email exists, a reset link has been sent");
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto)
        {
            var tokens = await _unitOfWork.PasswordResetTokens.FindAsync(t => t.Token == dto.Token && !t.IsUsed);
            var resetToken = tokens.FirstOrDefault();

            if (resetToken == null) return ApiResponse<bool>.Fail("Invalid or expired token");
            if (resetToken.ExpiresAt < DateTime.UtcNow) return ApiResponse<bool>.Fail("Token has expired");

            var user = await _unitOfWork.Users.GetByIdAsync(resetToken.UserId);
            if (user == null || !user.IsActive) return ApiResponse<bool>.Fail("User not found or disabled");

            user.PasswordHash = HashPassword(dto.NewPassword);
            await _unitOfWork.Users.UpdateAsync(user);

            resetToken.IsUsed = true;
            await _unitOfWork.PasswordResetTokens.UpdateAsync(resetToken);

            await RevokeAllTokensAsync(user.Id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Password reset successfully");
        }

        public async Task<ApiResponse<bool>> VerifyEmailAsync(VerifyEmailDto dto)
        {
            var tokens = await _unitOfWork.PasswordResetTokens.FindAsync(t => t.Token == dto.Token && !t.IsUsed);
            var verifyToken = tokens.FirstOrDefault();

            if (verifyToken == null) return ApiResponse<bool>.Fail("Invalid or expired token");
            if (verifyToken.ExpiresAt < DateTime.UtcNow) return ApiResponse<bool>.Fail("Token has expired");

            var user = await _unitOfWork.Users.GetByIdAsync(verifyToken.UserId);
            if (user == null) return ApiResponse<bool>.Fail("User not found");

            user.IsEmailVerified = true;
            await _unitOfWork.Users.UpdateAsync(user);

            verifyToken.IsUsed = true;
            await _unitOfWork.PasswordResetTokens.UpdateAsync(verifyToken);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Email verified successfully");
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private async Task RevokeAllTokensExceptAsync(Guid userId, Guid? excludeTokenId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens.FindAsync(t => t.UserId == userId && !t.IsRevoked);
            foreach (var token in activeTokens)
            {
                if (excludeTokenId.HasValue && token.Id == excludeTokenId.Value) continue;
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.RefreshTokens.UpdateAsync(token);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        private string GenerateAccessToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new Claim("role",   user.Role.ToString()),
                new Claim("userId", user.Id.ToString()),
            };

            // ✅ بس أضف tenantId لو موجود — السوبر أدمن مش بيبعتش claim خالص
            if (user.TenantId.HasValue)
                claims.Add(new Claim("tenantId", user.TenantId.Value.ToString()));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static (string plain, string hash) GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var plain = Convert.ToBase64String(randomBytes);
            return (plain, HashToken(plain));
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
        private static bool VerifyPassword(string p, string h) => BCrypt.Net.BCrypt.Verify(p, h);

        private async Task CleanupOldSessionsAsync(Guid userId)
        {
            var activeTokens = await _unitOfWork.RefreshTokens
                .FindAsync(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);

            var sorted = activeTokens.OrderBy(t => t.CreatedAt).ToList();
            if (sorted.Count >= MaxActiveSessionsPerUser)
            {
                foreach (var token in sorted.Take(sorted.Count - MaxActiveSessionsPerUser + 1))
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