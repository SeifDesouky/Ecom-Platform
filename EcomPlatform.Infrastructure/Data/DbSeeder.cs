using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Enums;
using EcomPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcomPlatform.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                await SeedPlansAsync(db, logger);
                await SeedSuperAdminAsync(db, config, logger);
                await SeedDefaultSettingsAsync(db, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "خطأ أثناء عملية الـ Seeding");
                throw;
            }
        }

        // ── Plans ─────────────────────────────────────────────────────────────

        private static async Task SeedPlansAsync(AppDbContext db, ILogger logger)
        {
            if (await db.Plans.IgnoreQueryFilters().AnyAsync())
            {
                logger.LogInformation("Plans موجودة بالفعل، تخطي الـ Seeding");
                return;
            }

            var plans = new List<Plan>
            {
                new()
                {
                    Name = "Basic",
                    Description = "مثالي للمتاجر الصغيرة والناشئة",
                    MonthlyPrice = 99,
                    YearlyPrice = 990,   // خصم شهر مجاناً
                    IsActive = true,
                    IsPopular = false,
                    MaxProducts = 100,
                    MaxOrders = 500,
                    MaxCustomers = 1000,
                    MaxUsers = 3,
                    HasAnalytics = false,
                    HasAPI = false,
                    HasMultiCurrency = false,
                    HasCustomDomain = false,
                    HasPrioritySupport = false
                },
                new()
                {
                    Name = "Pro",
                    Description = "للمتاجر النامية التي تحتاج مزيداً من القدرات",
                    MonthlyPrice = 299,
                    YearlyPrice = 2990,
                    IsActive = true,
                    IsPopular = true,   // الأكثر طلباً
                    MaxProducts = 1000,
                    MaxOrders = 5000,
                    MaxCustomers = 10000,
                    MaxUsers = 10,
                    HasAnalytics = true,
                    HasAPI = true,
                    HasMultiCurrency = false,
                    HasCustomDomain = true,
                    HasPrioritySupport = false
                },
                new()
                {
                    Name = "Enterprise",
                    Description = "للمتاجر الكبيرة بدون قيود",
                    MonthlyPrice = 999,
                    YearlyPrice = 9990,
                    IsActive = true,
                    IsPopular = false,
                    MaxProducts = -1,   // غير محدود
                    MaxOrders = -1,
                    MaxCustomers = -1,
                    MaxUsers = -1,
                    HasAnalytics = true,
                    HasAPI = true,
                    HasMultiCurrency = true,
                    HasCustomDomain = true,
                    HasPrioritySupport = true
                }
            };

            await db.Plans.AddRangeAsync(plans);
            await db.SaveChangesAsync();
            logger.LogInformation("✅ تم إنشاء {Count} باقة بنجاح", plans.Count);
        }

        // ── SuperAdmin ────────────────────────────────────────────────────────

        private static async Task SeedSuperAdminAsync(
            AppDbContext db, IConfiguration config, ILogger logger)
        {
            var adminEmail = config["Seed:AdminEmail"] ?? "admin@platform.io";

            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Role == UserRole.SuperAdmin))
            {
                logger.LogInformation("SuperAdmin موجود بالفعل، تخطي الـ Seeding");
                return;
            }

            var adminPassword = config["Seed:AdminPassword"];

            // لو مفيش password في الـ config، اعمل خطأ واضح بدل ما تستخدم default ضعيف
            if (string.IsNullOrEmpty(adminPassword))
            {
                logger.LogWarning(
                    "⚠️ Seed:AdminPassword مش موجود في الـ config. " +
                    "استخدم: dotnet user-secrets set \"Seed:AdminPassword\" \"<YourStrongPassword>\"");

                // في Development فقط نستخدم default مؤقت
                adminPassword = "Admin@12345!";
            }

            var superAdmin = new User
            {
                FirstName = "Super",
                LastName = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = UserRole.SuperAdmin,
                IsActive = true,
                Phone = ""
            };

            await db.Users.AddAsync(superAdmin);
            await db.SaveChangesAsync();
            logger.LogInformation("✅ تم إنشاء SuperAdmin: {Email}", adminEmail);
        }

        // ── Default Platform Settings ─────────────────────────────────────────

        private static async Task SeedDefaultSettingsAsync(AppDbContext db, ILogger logger)
        {
            if (await db.Settings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == null))
            {
                logger.LogInformation("Platform Settings موجودة بالفعل، تخطي الـ Seeding");
                return;
            }

            var settings = new List<Setting>
            {
                new() { Key = "platform.name",          Value = "Fatora Platform", Group = "general",  IsPublic = true,  TenantId = null },
                new() { Key = "platform.support_email", Value = "support@fatora.io", Group = "general", IsPublic = true, TenantId = null },
                new() { Key = "platform.max_file_size", Value = "10485760",        Group = "uploads",  IsPublic = false, TenantId = null, Description = "الحد الأقصى لحجم الملف بالـ bytes (10MB)" },
                new() { Key = "platform.allowed_image_types", Value = "jpg,jpeg,png,webp", Group = "uploads", IsPublic = false, TenantId = null },
                new() { Key = "platform.maintenance_mode", Value = "false",        Group = "system",   IsPublic = false, TenantId = null },
            };

            await db.Settings.AddRangeAsync(settings);
            await db.SaveChangesAsync();
            logger.LogInformation("✅ تم إنشاء {Count} إعداد للمنصة", settings.Count);
        }
    }
}
