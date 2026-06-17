using EcomPlatform.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace EcomPlatform.Application.DTOs.HelpCenter
{
    // ── Category Request DTOs ─────────────────────────────────────────────────
    public class CreateHelpCategoryDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // ✅ شيلنا [Required] — الـ Service بتولّده تلقائياً من الاسم لو جاء فاضي
        [MaxLength(200)]
        public string Slug { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
        public Guid? TenantId { get; set; }
    }

    public class UpdateHelpCategoryDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
    }

    // ── Article Request DTOs ──────────────────────────────────────────────────
    public class CreateHelpArticleDto
    {
        [Required, MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        // ✅ شيلنا [Required] — الـ Service بتولّده تلقائياً من العنوان لو جاء فاضي
        [MaxLength(500)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public string Excerpt { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsFAQ { get; set; } = false;
        public int SortOrder { get; set; } = 0;

        [Required]
        public Guid HelpCategoryId { get; set; }

        public Guid? AuthorId { get; set; }
        public Guid? TenantId { get; set; }
    }

    public class UpdateHelpArticleDto
    {
        [Required, MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public string Excerpt { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsFAQ { get; set; } = false;
        public int SortOrder { get; set; } = 0;
        public Guid HelpCategoryId { get; set; }
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────
    public class HelpCategoryResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public int ArticlesCount { get; set; }
        public Guid? TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<HelpArticleSummaryDto> Articles { get; set; } = new();
    }

    public class HelpArticleResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public ArticleStatus Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public bool IsFAQ { get; set; }
        public int SortOrder { get; set; }
        public int ViewCount { get; set; }
        public int HelpfulCount { get; set; }
        public int NotHelpfulCount { get; set; }
        public Guid HelpCategoryId { get; set; }
        public string HelpCategoryName { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class HelpArticleSummaryDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public bool IsFAQ { get; set; }
        public int ViewCount { get; set; }
        public int HelpfulCount { get; set; }
        public int SortOrder { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class HelpSearchResultDto
    {
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<HelpArticleSummaryDto> Articles { get; set; } = new();
    }
}