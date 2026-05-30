namespace EcomPlatform.Core.Enums
{
    public enum ReturnStatus
    {
        Pending = 1,   // انتظار مراجعة الـ Admin
        Approved = 2,   // وافق الـ Admin
        Rejected = 3,   // رفض الـ Admin
        Completed = 4,   // استلم المنتج + اكتمل الاسترداد
        Cancelled = 5,   // ألغاه العميل قبل المراجعة
    }

    public enum ReturnReason
    {
        DefectiveProduct = 1,   // منتج معيب
        WrongItem = 2,   // منتج غلط
        NotAsDescribed = 3,   // مش زي ما وصفه
        DamagedInShipping = 4,   // اتكسر في الشحن
        OrderCancelled = 5,   // الغاء الطلب (تلقائي)
        ChangedMind = 6,   // العميل غيّر رأيه
        Other = 99,
    }

    public enum ReturnInitiator
    {
        Customer = 1,
        Admin = 2,
        System = 3,   // تلقائي عند Cancel
    }

    public enum RefundStatus
    {
        Pending = 1,
        Processing = 2,
        Completed = 3,
        Failed = 4,
        Skipped = 5,   // مثلاً لو الأوردر مش Paid
    }

    public enum RefundMethod
    {
        Manual = 1,   // Admin يعمله يدوي
        Gateway = 2,   // تلقائي عبر بوابة الدفع
    }
}