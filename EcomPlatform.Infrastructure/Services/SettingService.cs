using EcomPlatform.Application.Common;
using EcomPlatform.Application.DTOs.Settings;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;

namespace EcomPlatform.Infrastructure.Services
{
    public class SettingService : ISettingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SettingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<SettingResponseDto>> CreateAsync(CreateSettingDto dto)
        {
            var existing = await _unitOfWork.Settings.FindAsync(s =>
                s.Key == dto.Key && s.TenantId == dto.TenantId);
            if (existing.Any())
                return ApiResponse<SettingResponseDto>.Fail("Setting key already exists");

            var setting = new Setting
            {
                Key = dto.Key,
                Value = dto.Value,
                Group = dto.Group,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                TenantId = dto.TenantId
            };

            await _unitOfWork.Settings.AddAsync(setting);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<SettingResponseDto>.Ok(MapToDto(setting), "Setting created successfully");
        }

        public async Task<ApiResponse<SettingResponseDto>> GetByKeyAsync(string key, Guid? tenantId)
        {
            var settings = await _unitOfWork.Settings.FindAsync(s =>
                s.Key == key && s.TenantId == tenantId);
            var setting = settings.FirstOrDefault();

            if (setting == null)
                return ApiResponse<SettingResponseDto>.Fail("Setting not found");

            return ApiResponse<SettingResponseDto>.Ok(MapToDto(setting));
        }

        public async Task<ApiResponse<IEnumerable<SettingGroupDto>>> GetAllByTenantAsync(Guid? tenantId)
        {
            var settings = await _unitOfWork.Settings.FindAsync(s => s.TenantId == tenantId);

            var groups = settings
                .GroupBy(s => s.Group)
                .Select(g => new SettingGroupDto
                {
                    Group = g.Key,
                    Settings = g.Select(MapToDto).ToList()
                });

            return ApiResponse<IEnumerable<SettingGroupDto>>.Ok(groups);
        }

        public async Task<ApiResponse<SettingResponseDto>> UpdateAsync(string key, UpdateSettingDto dto, Guid? tenantId)
        {
            var settings = await _unitOfWork.Settings.FindAsync(s =>
                s.Key == key && s.TenantId == tenantId);
            var setting = settings.FirstOrDefault();

            if (setting == null)
                return ApiResponse<SettingResponseDto>.Fail("Setting not found");

            setting.Value = dto.Value;
            await _unitOfWork.Settings.UpdateAsync(setting);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<SettingResponseDto>.Ok(MapToDto(setting), "Setting updated successfully");
        }

        public async Task<ApiResponse<bool>> BulkUpdateAsync(BulkUpdateSettingDto dto)
        {
            foreach (var item in dto.Settings)
            {
                var settings = await _unitOfWork.Settings.FindAsync(s =>
                    s.Key == item.Key && s.TenantId == dto.TenantId);
                var setting = settings.FirstOrDefault();

                if (setting != null)
                {
                    setting.Value = item.Value;
                    await _unitOfWork.Settings.UpdateAsync(setting);
                }
                else
                {
                    await _unitOfWork.Settings.AddAsync(new Setting
                    {
                        Key = item.Key,
                        Value = item.Value,
                        TenantId = dto.TenantId,
                        Group = "General"
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Settings updated successfully");
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            var setting = await _unitOfWork.Settings.GetByIdAsync(id);
            if (setting == null)
                return ApiResponse<bool>.Fail("Setting not found");

            await _unitOfWork.Settings.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<bool>.Ok(true, "Setting deleted successfully");
        }

        public async Task<ApiResponse<bool>> InitializeDefaultSettingsAsync(Guid tenantId)
        {
            var defaultSettings = new List<Setting>
            {
                // Store Settings
                new() { Key = "store_name", Value = "", Group = "Store", Description = "اسم المتجر", TenantId = tenantId },
                new() { Key = "store_email", Value = "", Group = "Store", Description = "البريد الإلكتروني", TenantId = tenantId },
                new() { Key = "store_phone", Value = "", Group = "Store", Description = "رقم الهاتف", TenantId = tenantId },
                new() { Key = "store_address", Value = "", Group = "Store", Description = "العنوان", TenantId = tenantId },
                new() { Key = "store_currency", Value = "EGP", Group = "Store", Description = "العملة", TenantId = tenantId },
                new() { Key = "store_language", Value = "ar", Group = "Store", Description = "اللغة", TenantId = tenantId },
                new() { Key = "store_logo", Value = "", Group = "Store", Description = "الشعار", TenantId = tenantId },

                // SEO Settings
                new() { Key = "seo_title", Value = "", Group = "SEO", Description = "عنوان الصفحة", TenantId = tenantId },
                new() { Key = "seo_description", Value = "", Group = "SEO", Description = "وصف الصفحة", TenantId = tenantId },
                new() { Key = "seo_keywords", Value = "", Group = "SEO", Description = "الكلمات المفتاحية", TenantId = tenantId },

                // Social Media
                new() { Key = "social_facebook", Value = "", Group = "Social", Description = "رابط Facebook", TenantId = tenantId },
                new() { Key = "social_instagram", Value = "", Group = "Social", Description = "رابط Instagram", TenantId = tenantId },
                new() { Key = "social_twitter", Value = "", Group = "Social", Description = "رابط Twitter", TenantId = tenantId },
                new() { Key = "social_whatsapp", Value = "", Group = "Social", Description = "رقم WhatsApp", TenantId = tenantId },

                // Payment Settings
                new() { Key = "payment_cod_enabled", Value = "true", Group = "Payment", Description = "الدفع عند الاستلام", TenantId = tenantId },
                new() { Key = "payment_online_enabled", Value = "false", Group = "Payment", Description = "الدفع الإلكتروني", TenantId = tenantId },

                // Email Settings
                new() { Key = "email_sender_name", Value = "", Group = "Email", Description = "اسم المرسل", TenantId = tenantId },
                new() { Key = "email_sender_address", Value = "", Group = "Email", Description = "بريد المرسل", TenantId = tenantId },
            };

            foreach (var setting in defaultSettings)
            {
                var existing = await _unitOfWork.Settings.FindAsync(s =>
                    s.Key == setting.Key && s.TenantId == tenantId);
                if (!existing.Any())
                    await _unitOfWork.Settings.AddAsync(setting);
            }

            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<bool>.Ok(true, "Default settings initialized successfully");
        }

        private static SettingResponseDto MapToDto(Setting setting) => new()
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Group = setting.Group,
            Description = setting.Description,
            IsPublic = setting.IsPublic,
            TenantId = setting.TenantId,
            CreatedAt = setting.CreatedAt
        };
    }
}