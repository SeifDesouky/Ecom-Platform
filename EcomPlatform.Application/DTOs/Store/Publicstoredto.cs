// ================================================================
// EcomPlatform.Application/DTOs/Store/PublicStoreDto.cs
// ================================================================
namespace EcomPlatform.Application.DTOs.Store
{
    public class PublicStoreDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThemeColor { get; set; } = string.Empty;
        public string Currency { get; set; } = "SAR";
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<PublicProductDto> FeaturedProducts { get; set; } = new();
        public List<PublicProductDto> AllProducts { get; set; } = new();
    }

    public class PublicProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public bool IsFeatured { get; set; }
        public int Stock { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<string> Images { get; set; } = new();
    }
}