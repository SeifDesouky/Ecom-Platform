using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Core.Enums;

namespace EcomPlatform.Core.Entities
{
    /// <summary>
    /// القيد المحاسبي — Double-Entry: مجموع المدين = مجموع الدائن دائماً.
    /// </summary>
    public class JournalEntry : BaseEntity, ITenantEntity
    {
        public string EntryNumber { get; set; } = string.Empty;  // JE-YYYYMMDD-XXXXXX
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;

        public JournalEntrySource Source { get; set; } = JournalEntrySource.Manual;
        public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

        // المرجع — معرف العملية المصدر
        public Guid? ReferenceId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty; // رقم الفاتورة أو الأوردر

        // مجاميع للتحقق السريع (Debit == Credit دائماً)
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        public string Notes { get; set; } = string.Empty;

        // من أنشأ القيد
        public Guid? CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        // القيد المعكوس (لو Reversed)
        public Guid? ReversedByEntryId { get; set; }

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }

    /// <summary>
    /// سطر واحد في القيد — إما مدين أو دائن.
    /// </summary>
    public class JournalEntryLine : BaseEntity
    {
        public Guid JournalEntryId { get; set; }
        public JournalEntry? JournalEntry { get; set; }

        public Guid AccountId { get; set; }
        public ChartOfAccount? Account { get; set; }

        public decimal Debit { get; set; } = 0;
        public decimal Credit { get; set; } = 0;

        public string Description { get; set; } = string.Empty;

        // للمرجع السريع بدون JOIN
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }
}
