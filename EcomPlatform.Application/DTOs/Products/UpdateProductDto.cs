namespace EcomPlatform.Application.DTOs.Products
{
    public class UpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
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
        public bool IsFeatured { get; set; }
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public Guid CategoryId { get; set; }
    }
}