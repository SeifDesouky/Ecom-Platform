using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.CMS
{
    public class CreatePageDto
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public PageType Type { get; set; } = PageType.Custom;
        public int SortOrder { get; set; } = 0;
        public Guid? TenantId { get; set; }
    }
}