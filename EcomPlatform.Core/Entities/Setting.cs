using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Setting : BaseEntity, ITenantEntity
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = false;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}