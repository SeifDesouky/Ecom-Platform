namespace EcomPlatform.Application.DTOs.Categories
{
    public class CategoryResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentName { get; set; }
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ProductsCount { get; set; }
    }
}