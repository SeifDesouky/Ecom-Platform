using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    public class Product : BaseEntity, ITenantEntity
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
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public ProductStatus Status { get; set; } = ProductStatus.Active;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public decimal Weight { get; set; }

        // Relations
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public Guid CategoryId { get; set; }
        public Category? Category { get; set; }

        // Navigation
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}