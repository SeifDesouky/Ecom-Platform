using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Article : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string CoverImage { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
        public string Tags { get; set; } = string.Empty;
        public int ViewCount { get; set; } = 0;

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public Guid AuthorId { get; set; }
        public User? Author { get; set; }
    }
}