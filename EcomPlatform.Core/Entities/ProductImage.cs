using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    public class ProductImage : BaseEntity
    {
        public string Url { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
        public bool IsMain { get; set; } = false;

        public Guid ProductId { get; set; }
        public Product? Product { get; set; }
    }
}