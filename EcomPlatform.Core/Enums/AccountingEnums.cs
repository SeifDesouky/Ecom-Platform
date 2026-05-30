namespace EcomPlatform.Core.Enums
{
    /// <summary>نوع الحساب في شجرة الحسابات</summary>
    public enum AccountType
    {
        Asset = 1,   // أصول
        Liability = 2,   // التزامات
        Equity = 3,   // حقوق الملكية
        Revenue = 4,   // إيرادات
        Expense = 5,   // مصاريف
    }

    /// <summary>الطبيعة الطبيعية للحساب (Debit أو Credit)</summary>
    public enum AccountNature
    {
        Debit = 1,
        Credit = 2,
    }

    /// <summary>مصدر القيد — من أي عملية تم إنشاؤه</summary>
    public enum JournalEntrySource
    {
        Manual = 1,
        Invoice = 2,
        Order = 3,
        Refund = 4,
        StockMovement = 5,
        Subscription = 6,
    }

    /// <summary>حالة القيد</summary>
    public enum JournalEntryStatus
    {
        Draft = 1,
        Posted = 2,   // معتمد ومؤثر على الأرصدة
        Reversed = 3,   // تم عكسه
    }
}
