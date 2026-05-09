namespace EcomPlatform.Application.DTOs.Settings
{
    public class CreateSettingDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = false;
        public Guid? TenantId { get; set; }
    }

    public class UpdateSettingDto
    {
        public string Value { get; set; } = string.Empty;
        public Guid UpdatedById { get; set; }  // ← جديد
    }

    public class BulkUpdateSettingDto
    {
        public Dictionary<string, string> Settings { get; set; } = new();
        public Guid? TenantId { get; set; }
        public Guid UpdatedById { get; set; }  // ← جديد
    }

    public class SettingResponseDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SettingGroupDto
    {
        public string Group { get; set; } = string.Empty;
        public List<SettingResponseDto> Settings { get; set; } = new();
    }
}