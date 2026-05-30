using EcomPlatform.Core.Entities.Common;

namespace EcomPlatform.Core.Entities
{
    /// <summary>تصنيف مركز المساعدة — مثلاً: الدفع، الشحن، الحساب</summary>
    public class HelpCategory : BaseEntity, ITenantEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;       // اسم الأيقونة
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // null = منصة عامة، له قيمة = خاص بمتجر معين
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public ICollection<HelpArticle> Articles { get; set; } = new List<HelpArticle>();
    }
}
