using Asp.Versioning;
using EcomPlatform.API.Extensions;
using EcomPlatform.API.Middlewares;
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Application.Validators;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Infrastructure.Adapters;
using EcomPlatform.Infrastructure.Adapters.AliExpress;
using EcomPlatform.Infrastructure.Adapters.Amazon;
using EcomPlatform.Infrastructure.Adapters.BigCommerce;
using EcomPlatform.Infrastructure.Adapters.eBay;
using EcomPlatform.Infrastructure.Adapters.Etsy;
using EcomPlatform.Infrastructure.Adapters.ExpandCart;
using EcomPlatform.Infrastructure.Adapters.ExpandCartEgypt;
using EcomPlatform.Infrastructure.Adapters.FacebookShop;
using EcomPlatform.Infrastructure.Adapters.GoogleShopping;
using EcomPlatform.Infrastructure.Adapters.InstagramShop;
using EcomPlatform.Infrastructure.Adapters.Jarir;
using EcomPlatform.Infrastructure.Adapters.Lazada;
using EcomPlatform.Infrastructure.Adapters.Magento;
using EcomPlatform.Infrastructure.Adapters.Meta;
using EcomPlatform.Infrastructure.Adapters.Noon;
using EcomPlatform.Infrastructure.Adapters.NoonExpress;
using EcomPlatform.Infrastructure.Adapters.NotSupported;
using EcomPlatform.Infrastructure.Adapters.Salla;
using EcomPlatform.Infrastructure.Adapters.Shein;
using EcomPlatform.Infrastructure.Adapters.Shopee;
using EcomPlatform.Infrastructure.Adapters.Shopify;
using EcomPlatform.Infrastructure.Adapters.Squarespace;
using EcomPlatform.Infrastructure.Adapters.TikTokShop;
using EcomPlatform.Infrastructure.Adapters.Walmart;
using EcomPlatform.Infrastructure.Adapters.WhatsAppCatalog;
using EcomPlatform.Infrastructure.Adapters.WooCommerce;
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
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

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
// YouCan
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
builder.Services.AddScoped<IMarketplaceAdapter, JarirAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, SheinAdapter>();
// Noon
builder.Services.AddHttpClient<NoonAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, NoonAdapter>();
// TikTok Shop
builder.Services.AddHttpClient<TikTokShopAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, TikTokShopAdapter>();
// Google Shopping
builder.Services.AddHttpClient<GoogleShoppingAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, GoogleShoppingAdapter>();
// Instagram Shop
builder.Services.AddHttpClient<InstagramShopAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, InstagramShopAdapter>();
// WhatsApp Catalog
builder.Services.AddHttpClient<WhatsAppCatalogAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, WhatsAppCatalogAdapter>();
// Amazon — timeout 60s بسبب SigV4 overhead
builder.Services.AddHttpClient<AmazonAdapter>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<IMarketplaceAdapter, AmazonAdapter>();
// eBay — 30s
builder.Services.AddHttpClient<EbayAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, EbayAdapter>();
// Walmart — 30s (polling فقط، مفيش webhooks)
builder.Services.AddHttpClient<WalmartAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, WalmartAdapter>();
// Etsy — polling فقط، مفيش webhooks
builder.Services.AddHttpClient<EtsyAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, EtsyAdapter>();
// WooCommerce
builder.Services.AddHttpClient<WooCommerceAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, WooCommerceAdapter>();
// Magento — polling فقط، مفيش webhooks
builder.Services.AddHttpClient<MagentoAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, MagentoAdapter>();
// BigCommerce
builder.Services.AddHttpClient<BigCommerceAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, BigCommerceAdapter>();
// Squarespace
builder.Services.AddHttpClient<SquarespaceAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, SquarespaceAdapter>();
// Lazada
builder.Services.AddHttpClient<LazadaAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, LazadaAdapter>();
// Shopee
builder.Services.AddHttpClient<ShopeeAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, ShopeeAdapter>();
// Facebook Shop
builder.Services.AddHttpClient<FacebookShopAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, FacebookShopAdapter>();
// AliExpress
builder.Services.AddHttpClient<AliExpressAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, AliExpressAdapter>();
// Noon Express
builder.Services.AddHttpClient<NoonExpressAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<IMarketplaceAdapter, NoonExpressAdapter>();
// ExpandCart Egypt
builder.Services.AddHttpClient<ExpandCartEgyptAdapter>();
builder.Services.AddScoped<IMarketplaceAdapter, ExpandCartEgyptAdapter>();
// Noon Express
builder.Services.AddScoped<NoonExpressWebhookProcessor>();
// YouCan
builder.Services.AddScoped<YouCanWebhookProcessor>();
// ExpandCart
builder.Services.AddScoped<ExpandCartWebhookProcessor>();

// AdapterFactory: Scoped لأن الـ adapters بتاعتها Scoped
builder.Services.AddScoped<IAdapterFactory, AdapterFactory>();

// ── Webhook Processors ────────────────────────────────────────────────────
// Salla
builder.Services.AddScoped<SallaOAuthService>();
builder.Services.AddScoped<SallaWebhookProcessor>();
// Zid
builder.Services.AddScoped<ZidWebhookProcessor>();
// Shopify
builder.Services.AddScoped<ShopifyWebhookProcessor>();
// WooCommerce
builder.Services.AddScoped<WooCommerceWebhookProcessor>();
// TikTok Shop
builder.Services.AddScoped<TikTokShopWebhookProcessor>();
// Meta (Instagram Shop + Facebook Shop + WhatsApp Catalog) — shared processor
builder.Services.AddScoped<MetaWebhookProcessor>();
// Amazon SNS
builder.Services.AddScoped<AmazonWebhookProcessor>();
// eBay
builder.Services.AddScoped<EbayWebhookProcessor>();
// Noon
builder.Services.AddScoped<NoonWebhookProcessor>();
// AliExpress
builder.Services.AddScoped<AliExpressWebhookProcessor>();
// Google Shopping
builder.Services.AddScoped<GoogleShoppingWebhookProcessor>();

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