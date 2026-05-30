using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// شجرة الحسابات — كل tenant عنده شجرة مستقلة تُهيّأ بالأكاونت الافتراضية عند الإنشاء.
    /// </summary>
    public class ChartOfAccount : BaseEntity, ITenantEntity
    {
        public string Code { get; set; } = string.Empty;       // كود الحساب: 1100، 4000، ...
        public string Name { get; set; } = string.Empty;       // اسم الحساب
        public string NameEn { get; set; } = string.Empty;     // اسم إنجليزي اختياري
        public string Description { get; set; } = string.Empty;

        public AccountType Type { get; set; }
        public AccountNature Nature { get; set; }

        public bool IsActive { get; set; } = true;
        public bool IsSystem { get; set; } = false;            // حسابات النظام — لا تُحذف

        // الحساب الأب (للتسلسل الهرمي)
        public Guid? ParentId { get; set; }
        public ChartOfAccount? Parent { get; set; }

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public ICollection<ChartOfAccount> Children { get; set; } = new List<ChartOfAccount>();
        public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }
}
