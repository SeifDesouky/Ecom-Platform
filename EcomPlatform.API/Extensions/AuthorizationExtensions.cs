// ============================================================
// المكان: EcomPlatform.API/Extensions/AuthorizationExtensions.cs
// ============================================================

using EcomPlatform.Application.Common;

namespace EcomPlatform.API.Extensions
{
    public static class AuthorizationExtensions
    {
        public static IServiceCollection AddRbacAuthorization(
            this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // SuperAdmin فقط
                options.AddPolicy(Policies.SuperAdminOnly, policy =>
                    policy.RequireRole("SuperAdmin"));

                // TenantAdmin وفوق
                options.AddPolicy(Policies.TenantAdminOrAbove, policy =>
                    policy.RequireRole("SuperAdmin", "TenantAdmin"));

                // Staff وفوق (كل موظفين المتجر)
                options.AddPolicy(Policies.TenantStaffOrAbove, policy =>
                    policy.RequireRole("SuperAdmin", "TenantAdmin", "TenantStaff"));

                // أي user مسجل
                options.AddPolicy(Policies.AnyAuthenticatedUser, policy =>
                    policy.RequireAuthenticatedUser());
            });

            return services;
        }
    }
}