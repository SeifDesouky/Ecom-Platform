namespace EcomPlatform.Core.Enums
{
    public enum ReviewStatus
    {
        Pending = 1,   // في الانتظار — المراجعة الافتراضية
        Approved = 2,   // معتمد ويظهر للعملاء
        Rejected = 3,   // مرفوض
        Spam = 4    // سبام
    }
}