namespace EcomPlatform.Core.Enums
{
    public enum StockMovementType
    {
        Purchase = 1,       // استلام بضاعة من مورد
        Sale = 2,           // بيع عبر المتجر أو POS
        Return = 3,         // إرجاع من عميل
        Adjustment = 4,     // تسوية يدوية (جرد)
        Transfer = 5,       // نقل بين مستودعين
        Damage = 6,         // بضاعة تالفة
        InitialStock = 7,   // رصيد افتتاحي
    }
}
