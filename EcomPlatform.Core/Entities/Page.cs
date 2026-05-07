using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;
using System.Security.AccessControl;

namespace EcomPlatform.Core.Entities
{
    public class Page : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public PageType Type { get; set; } = PageType.Custom;
        public int SortOrder { get; set; } = 0;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}