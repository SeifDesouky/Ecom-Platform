namespace EcomPlatform.Application.DTOs.Categories
{
    public class UpdateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }
}