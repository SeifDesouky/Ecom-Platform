namespace EcomPlatform.Core.Enums
{
    public enum UserRole
    {
        SuperAdmin = 1,    // صاحب المنصة الأم
        TenantAdmin = 2,   // صاحب المتجر
        TenantStaff = 3,   // موظف في المتجر
        Customer = 4       // عميل
    }
}