namespace EcomPlatform.Core.Enums
{
    public enum LoyaltyTransactionType
    {
        Earned = 1,  // ربح من شراء
        Redeemed = 2,  // صرف كخصم على أوردر
        Expired = 3,  // انتهت صلاحية
        Adjusted = 4,  // تعديل يدوي من الأدمن
        Bonus = 5,  // هدية / مكافأة
        Refunded = 6   // إعادة نقاط بعد إرجاع أوردر
    }
}
