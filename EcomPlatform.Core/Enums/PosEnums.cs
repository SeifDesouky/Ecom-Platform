namespace EcomPlatform.Core.Enums
{
    public enum PosSessionStatus
    {
        Open = 1,
        Closed = 2,
        Suspended = 3   // مفتوحة لكن الكاشير مش شغال دلوقتي
    }

    public enum PosPaymentMethod
    {
        Cash = 1,
        Card = 2,
        Mixed = 3,      // نقدي + كارت في نفس الوقت
        Loyalty = 4,    // نقاط ولاء
        Other = 5
    }

    public enum PosOrderStatus
    {
        Draft = 1,      // جاري الإضافة (الشاشة مفتوحة)
        Completed = 2,  // دُفع وانتهى
        Voided = 3,     // أُلغي قبل الدفع
        Refunded = 4    // استُرجع بعد الإتمام
    }
}
