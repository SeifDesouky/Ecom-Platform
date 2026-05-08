// ============================================================
// المكان: EcomPlatform.Application/Common/Policies.cs
// ============================================================

namespace EcomPlatform.Application.Common
{
    /// <summary>
    /// أسماء الـ Policies — بتستخدمها في [Authorize(Policy = "...")]
    /// </summary>
    public static class Policies
    {
        // فقط SuperAdmin (إدارة المنصة نفسها)
        public const string SuperAdminOnly = "SuperAdminOnly";

        // SuperAdmin أو TenantAdmin (إدارة المتجر)
        public const string TenantAdminOrAbove = "TenantAdminOrAbove";

        // SuperAdmin أو TenantAdmin أو TenantStaff (موظفين المتجر)
        public const string TenantStaffOrAbove = "TenantStaffOrAbove";

        // أي user مسجل دخول (حتى Customer)
        public const string AnyAuthenticatedUser = "AnyAuthenticatedUser";
    }
}