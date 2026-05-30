namespace EcomPlatform.Core.Enums
{
    public enum PaymentLinkType
    {
        FreeAmount = 1,   // مبلغ حر — المنشئ بيحدد المبلغ مسبقاً
        ProductBased = 2,   // قائمة منتجات محددة
        OrderBased = 3,   // مرتبط بأوردر موجود
    }

    public enum PaymentLinkStatus
    {
        Active = 1,   // شغال ويقبل دفع
        Inactive = 2,   // موقوف يدوياً
        Expired = 3,   // انتهت صلاحيته (تاريخ أو max uses)
        Paid = 4,   // اتدفع بالكامل (single-use مثلاً)
    }
}
