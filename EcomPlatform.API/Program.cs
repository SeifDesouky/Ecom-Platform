using Asp.Versioning;
using EcomPlatform.API.Extensions;
using EcomPlatform.API.Middlewares;
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Application.Validators;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters;
using EcomPlatform.Infrastructure.Adapters.ExpandCart;
using EcomPlatform.Infrastructure.Adapters.NotSupported;
using EcomPlatform.Infrastructure.Adapters.Salla;
using EcomPlatform.Infrastructure.Adapters.Shopify;
using EcomPlatform.Infrastructure.Adapters.YouCan;
using EcomPlatform.Infrastructure.Adapters.Zid;
using EcomPlatform.Infrastructure.Data;
using EcomPlatform.Infrastructure.Data.Interceptors;
using EcomPlatform.Infrastructure.Jobs;
using EcomPlatform.Infrastructure.Repositories;
using EcomPlatform.Infrastructure.Services;
using EcomPlatform.Shared.Settings;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("login", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("general", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });
});

builder.Services.AddOpenApiDocument(config =>
{
    config.Title = "EcomPlatform API";
    config.Version = "v1";

    config.AddSecurity("Bearer", Enumerable.Empty<string>(),
        new OpenApiSecurityScheme
        {
            Type = OpenApiSecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            Description = "Enter JWT Token"
        });

    config.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security
            .AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

builder.Services.AddScoped<ITenantProvider, CurrentTenantProvider>();
builder.Services.AddScoped<TenantEnforcementInterceptor>();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider
        .GetRequiredService<TenantEnforcementInterceptor>();

    options
        .UseMySQL(connectionString!)
        .AddInterceptors(interceptor);
});

var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()
    ?? throw new Exception("JwtSettings not found");

builder.Services.AddSingleton(jwtSettings);

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddScoped<IFileUploadService, CloudinaryFileUploadService>();

builder.Services.Configure<GoogleAuthSettings>(
    builder.Configuration.GetSection("GoogleAuth"));

builder.Services.Configure<AppleAuthSettings>(
    builder.Configuration.GetSection("AppleAuth"));

// ── Sync + Webhook Settings ───────────────────────────────────────────────
builder.Services.Configure<SyncSettings>(
    builder.Configuration.GetSection("SyncSettings"));

builder.Services.Configure<WebhookSettings>(
    builder.Configuration.GetSection("WebhookSettings"));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                RoleClaimType = "role",
                NameClaimType = "userId"
            };
    });

builder.Services.AddRbacAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:4200",
                "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddHealthChecks()
    .AddCheck("database", () =>
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

// ── Core Services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITenantDomainService, TenantDomainService>();
builder.Services.AddScoped<ICMSService, CMSService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IZatcaService, ZatcaService>();
builder.Services.AddHttpClient("ZatcaClient");
builder.Services.AddHostedService<DashboardSnapshotService>();

// ── Integrations ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();

// ── Marketplace Adapters ──────────────────────────────────────────────────
builder.Services.AddHttpClient<SallaAuthService>();
builder.Services.AddScoped<SallaAuthService>();

builder.Services.AddHttpClient<SallaAdapter>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IMarketplaceAdapter, SallaAdapter>();

// ZidAdapter
builder.Services.AddHttpClient<ZidAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ZidAdapter>();
// ShopifyAdapter
builder.Services.AddHttpClient<ShopifyAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ShopifyAdapter>();
// Yoycan
builder.Services.AddHttpClient<YouCanAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, YouCanAdapter>();
// ExpandCartAdapter
builder.Services.AddHttpClient<ExpandCartAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ExpandCartAdapter>();
// Not Supported — مفيش Public API
builder.Services.AddScoped<IMarketplaceAdapter, MatjarAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, TaggerAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ToggarAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ShopiniAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, PaycornStoreAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, MakhazinAdapter>();

// AdapterFactory: Scoped لأن الـ adapters بتاعتها Scoped
builder.Services.AddScoped<IAdapterFactory, AdapterFactory>();

// Salla OAuth + Webhooks
builder.Services.AddScoped<SallaOAuthService>();
builder.Services.AddScoped<SallaWebhookProcessor>();

// Zid Webhooks
builder.Services.AddScoped<ZidWebhookProcessor>();

// ── Background Jobs ───────────────────────────────────────────────────────
builder.Services.AddHostedService<BackgroundSyncJob>();

var app = builder.Build();

app.UseOpenApi();
app.UseSwaggerUi(settings =>
{
    settings.DocumentTitle = "EcomPlatform API";
});

if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

app.UseRateLimiter();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = $"{entry.Value.Duration.TotalMilliseconds} ms"
            }),
            totalDuration = $"{report.TotalDuration.TotalMilliseconds} ms"
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Migration failed: {Message}", ex.Message);
    }
}

try
{
    await DbSeeder.SeedAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services
        .GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Seeding failed: {Message}", ex.Message);
}

await app.RunAsync();