namespace EcomPlatform.Application.DTOs.Products
{
    public class CreateProductDto
    {
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
        public int LowStockAlert { get; set; } = 5;
        public bool TrackInventory { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public Guid TenantId { get; set; }
        public Guid CategoryId { get; set; }
        public List<ProductImageDto> Images { get; set; } = new();
    }

    public class ProductImageDto
    {
        public string Url { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
        public bool IsMain { get; set; } = false;
    }
}