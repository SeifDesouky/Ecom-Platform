// ================================================================
// EcomPlatform.Infrastructure/Services/StoreService.cs
// ================================================================
using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Auth;
using EcomPlatform.Application.DTOs.Store;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Data;
using EcomPlatform.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomPlatform.Infrastructure.Services
{
    public class StoreService : IStoreService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppDbContext _dbContext;
        private readonly JwtSettings _jwtSettings;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;
        private readonly ISettingService _settingService; // ✅ [NEW]

        // ✅ [2] Reserved slugs — تمنع حجز أسماء النظام
        private static readonly HashSet<string> ReservedSlugs =
        [
            "admin", "api", "www", "app", "mail", "store", "help",
            "support", "billing", "dashboard", "login", "register",
            "signup", "auth", "static", "assets", "cdn", "blog",
            "status", "dev", "staging", "test", "demo", "sandbox"
        ];

        public StoreService(
            IUnitOfWork unitOfWork,
            AppDbContext dbContext,
            JwtSettings jwtSettings,
            IAuditLogService auditLogService,
            IEmailService emailService,
            ISettingService settingService) // ✅ [NEW]
        {
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _jwtSettings = jwtSettings;
            _auditLogService = auditLogService;
            _emailService = emailService;
            _settingService = settingService; // ✅ [NEW]
        }

        // ── Register Store ────────────────────────────────────────────────────

        public async Task<ApiResponse<AuthResponseDto>> RegisterStoreAsync(RegisterStoreDto dto)
        {
            // ── 1. تأكد إن الـ Email مش مستخدم ──────────────────────────────
            var existingUsers = await _unitOfWork.Users.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                return ApiResponse<AuthResponseDto>.Fail("This email is already registered");

            // ── 2. تأكد إن الـ Slug متاح (reserved + DB check) ───────────────
            var slugCheck = await CheckSlugAvailabilityAsync(dto.Slug);
            if (!slugCheck.IsAvailable)
                return ApiResponse<AuthResponseDto>.Fail(slugCheck.Message ?? "This store URL is not available");

            // ── 3. ابدأ Transaction ───────────────────────────────────────────
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // ── 4. إنشاء الـ Tenant ───────────────────────────────────────
                var tenant = new Tenant
                {
                    Name = dto.StoreName,
                    Slug = dto.Slug.ToLowerInvariant(),
                    Email = dto.Email,
                    Phone = dto.Phone ?? string.Empty,
                    Logo = dto.Logo ?? string.Empty,
                    Domain = dto.Domain ?? string.Empty,
                    Description = dto.Description,
                    ThemeColor = dto.ThemeColor,
                    IsActive = true,
                    Status = TenantStatus.Active,
                    SubscriptionEndDate = DateTime.UtcNow.AddDays(14),
                };

                await _unitOfWork.Tenants.AddAsync(tenant);
                await _unitOfWork.SaveChangesAsync();

                // ── 4b. تهيئة الإعدادات الافتراضية تلقائيًا من بيانات المتجر ────
                // ✅ [NEW] بدل ما المستخدم يضغط "تهيئة الإعدادات" يدويًا ويلاقيها فاضية
                await _settingService.InitializeDefaultSettingsAsync(tenant.Id);

                // ── 5. إنشاء الـ TenantAdmin User ────────────────────────────
                var user = new User
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Email = dto.Email,
                    Phone = dto.Phone ?? string.Empty,
                    PasswordHash = HashPassword(dto.Password),
                    Role = UserRole.TenantAdmin,
                    IsActive = true,
                    IsEmailVerified = false,
                    TenantId = tenant.Id,
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.SaveChangesAsync();

                // ── 6. إنشاء UserProfile ──────────────────────────────────────
                var profile = new UserProfile { UserId = user.Id };
                await _unitOfWork.UserProfiles.AddAsync(profile);

                // ── 7. إصدار Refresh Token ────────────────────────────────────
                var (plainToken, tokenHash) = GenerateRefreshToken();
                var refreshToken = new RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryInDays),
                };

                await _unitOfWork.RefreshTokens.AddAsync(refreshToken);
                await _unitOfWork.SaveChangesAsync();

                // ── 8. Commit Transaction ─────────────────────────────────────
                await transaction.CommitAsync();

                // ── 9. Welcome Email (fire-and-forget بعد الـ commit) ─────────
                // ✅ [3] بنبعت welcome email باسم المتجر
                _ = _emailService.SendWelcomeAsync(
                    to: user.Email,
                    name: $"{user.FirstName} {user.LastName}".Trim(),
                    tenantName: tenant.Name);

                // ── 10. Audit Log ─────────────────────────────────────────────
                await _auditLogService.LogAsync(
                    entityName: "Tenant",
                    entityId: tenant.Id.ToString(),
                    action: AuditAction.Create,
                    userId: user.Id,
                    tenantId: tenant.Id,
                    newValue: $"Self-service store '{tenant.Name}' (slug: {tenant.Slug}) created by {user.Email}");

                // ── 11. إصدار JWT وإرجاع الـ Response ────────────────────────
                var accessToken = GenerateAccessToken(user);

                return ApiResponse<AuthResponseDto>.Ok(new AuthResponseDto
                {
                    Token = accessToken,
                    RefreshToken = plainToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                    User = new UserDto
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                        TenantId = user.TenantId,
                    }
                }, "Store created successfully! Welcome aboard 🎉");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ── Check Slug Availability ───────────────────────────────────────────

        public async Task<SlugAvailabilityResponseDto> CheckSlugAvailabilityAsync(string slug)
        {
            slug = slug.ToLowerInvariant().Trim();

            // ✅ [2a] تحقق من الـ format
            if (!Regex.IsMatch(slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            {
                return new SlugAvailabilityResponseDto
                {
                    Slug = slug,
                    IsAvailable = false,
                    Message = "Slug must contain only lowercase letters, numbers, and hyphens"
                };
            }

            // ✅ [2b] تحقق من Reserved slugs
            if (ReservedSlugs.Contains(slug))
            {
                return new SlugAvailabilityResponseDto
                {
                    Slug = slug,
                    IsAvailable = false,
                    Message = "This URL is reserved and cannot be used"
                };
            }

            // ✅ [2c] تحقق من الـ DB — IgnoreQueryFilters يشوف soft-deleted كمان
            var taken = await _dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Slug == slug);

            return new SlugAvailabilityResponseDto
            {
                Slug = slug,
                IsAvailable = !taken,
                Message = taken ? "This URL is already taken" : "This URL is available"
            };
        }

        // ── Get Public Store ──────────────────────────────────────────────────

        public async Task<ApiResponse<PublicStoreDto>> GetPublicStoreAsync(string slug)
        {
            slug = slug.ToLowerInvariant().Trim();

            // 1. جيب الـ Tenant بالـ slug
            var tenants = await _unitOfWork.Tenants.FindAsync(t =>
                t.Slug == slug && t.IsActive);
            var tenant = tenants.FirstOrDefault();
            if (tenant == null)
                return ApiResponse<PublicStoreDto>.Fail("Store not found");

            // 2. جيب إعدادات المتجر (currency)
            var settings = await _unitOfWork.Settings.FindAsync(s =>
                s.TenantId == tenant.Id && !s.IsDeleted);
            var settingsMap = settings.ToDictionary(s => s.Key, s => s.Value);

            // 3. جيب المنتجات النشطة
            var products = await _unitOfWork.Products.FindAsync(p =>
                p.TenantId == tenant.Id &&
                p.IsActive &&
                p.Status == ProductStatus.Active);

            var productDtos = products.Select(p => new PublicProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Description = p.Description,
                ShortDescription = p.ShortDescription,
                Price = p.Price,
                ComparePrice = p.ComparePrice,
                IsFeatured = p.IsFeatured,
                Stock = p.Stock,
                CategoryName = p.Category?.Name ?? string.Empty,
                Images = p.Images.Select(i => i.Url).ToList(),
            }).ToList();

            var dto = new PublicStoreDto
            {
                Name = tenant.Name,
                Slug = tenant.Slug,
                Logo = tenant.Logo,
                Description = tenant.Description ?? string.Empty,
                ThemeColor = tenant.ThemeColor ?? "#3B82F6",
                Currency = settingsMap.GetValueOrDefault("store_currency", "SAR"),
                Phone = tenant.Phone,
                Email = tenant.Email,
                FeaturedProducts = productDtos.Where(p => p.IsFeatured).ToList(),
                AllProducts = productDtos,
            };

            return ApiResponse<PublicStoreDto>.Ok(dto);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

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
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            var plain = Convert.ToBase64String(bytes);
            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(plain))).ToLowerInvariant();
            return (plain, hash);
        }

        private static string HashPassword(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password);
    }
}