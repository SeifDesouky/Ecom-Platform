namespace EcomPlatform.Application.DTOs.CMS
{
    public class CreateArticleDto
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string CoverImage { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public string Tags { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public Guid AuthorId { get; set; }
    }
}