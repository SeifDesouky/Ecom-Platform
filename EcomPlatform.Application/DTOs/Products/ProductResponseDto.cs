using EcomPlatform.Core.Enums;

namespace EcomPlatform.Application.DTOs.Products
{
    public class ProductResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? ComparePrice { get; set; }
        public decimal? CostPrice { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int LowStockAlert { get; set; }
        public bool TrackInventory { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public ProductStatus Status { get; set; }
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public Guid TenantId { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<ProductImageResponseDto> Images { get; set; } = new();
    }

    public class ProductImageResponseDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsMain { get; set; }
    }
}