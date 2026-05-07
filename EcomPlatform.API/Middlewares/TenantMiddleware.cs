using System.Security.Claims;
using EcomPlatform.Application.Common.Interfaces;

namespace EcomPlatform.API.Middlewares
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITenantProvider tenantProvider)
        {
            var tenantClaim = context.User.FindFirst("tenantId")?.Value;

            if (!string.IsNullOrEmpty(tenantClaim)
                && Guid.TryParse(tenantClaim, out var tenantId))
            {
                tenantProvider.SetTenant(tenantId);
            }

            await _next(context);
        }
    }
}