using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public Guid? ParentId { get; set; }
        public Category? Parent { get; set; }
        public Guid TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Navigation
        public ICollection<Category> Children { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}