using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>مقالة في مركز المساعدة أو FAQ</summary>
    public class HelpArticle : BaseEntity, ITenantEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;    // HTML
        public string Excerpt { get; set; } = string.Empty;    // ملخص قصير
        public string Tags { get; set; } = string.Empty;       // مفصولة بفاصلة
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
        public bool IsFAQ { get; set; } = false;               // هل هو FAQ أم مقالة عادية؟
        public int SortOrder { get; set; } = 0;
        public int ViewCount { get; set; } = 0;
        public int HelpfulCount { get; set; } = 0;
        public int NotHelpfulCount { get; set; } = 0;
        public DateTime? PublishedAt { get; set; }

        // Relations
        public Guid HelpCategoryId { get; set; }
        public HelpCategory? HelpCategory { get; set; }

        public Guid? AuthorId { get; set; }
        public User? Author { get; set; }

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
