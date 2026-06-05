using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
            ITenantProvider tenantProvider,
            AppDbContext dbContext)
        {
            var tenantValue = context.Request.Headers["X-Tenant-ID"].FirstOrDefault()
                ?? context.User.FindFirst("tenantId")?.Value;

            if (!string.IsNullOrWhiteSpace(tenantValue)
                && Guid.TryParse(tenantValue, out var tenantId))
            {
                tenantProvider.SetTenant(tenantId);

                context.Items["store_context"] = tenantId;

                if (ShouldSkipStoreInitializationCheck(context.Request.Path))
                {
                    await _next(context);
                    return;
                }

                var hasStoreSettings = await dbContext.Settings
                    .IgnoreQueryFilters()
                    .AnyAsync(setting => setting.TenantId == tenantId, context.RequestAborted);

                if (!hasStoreSettings)
                {
                    await WriteStoreNotInitializedAsync(context, tenantId);
                    return;
                }
            }

            await _next(context);
        }

        private static bool ShouldSkipStoreInitializationCheck(PathString path)
        {
            return path.StartsWithSegments("/api/v1/settings", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/v1/inventory", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/v1/accounting", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/v1/dashboard", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteStoreNotInitializedAsync(
            HttpContext context,
            Guid tenantId)
        {
            context.Response.StatusCode = StatusCodes.Status424FailedDependency;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                error = "STORE_NOT_INITIALIZED",
                message = "Store settings have not been initialized. Please contact support.",
                tenantId = tenantId.ToString()
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}