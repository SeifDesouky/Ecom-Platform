using EcomPlatform.Infrastructure.Data;
using EcomPlatform.Infrastructure.Repositories;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EcomPlatform.Application.Services.Interfaces;
using EcomPlatform.Infrastructure.Services;
using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.API.Middlewares;
using EcomPlatform.API.Extensions;
using FluentValidation;
using FluentValidation.AspNetCore;
using EcomPlatform.Application.Validators;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

// ============================
// Controllers
// ============================
builder.Services.AddControllers();

// ============================
// API Versioning
// ============================
builder.Services.AddApiVersioning(options =>
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

// ============================
// FluentValidation
// ============================
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginValidator>();

// ============================
// Rate Limiting
// ============================
builder.Services.AddRateLimiter(options =>
{
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

    options.RejectionStatusCode = 429;
});

// ============================
// NSwag
// ============================
builder.Services.AddOpenApiDocument(c =>
{
    c.Title = "EcomPlatform API";
    c.Version = "v1";

    c.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });

    c.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security
            .AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});

// ============================
// Database
// ============================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================
// JWT Settings
// ============================
var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()!;

builder.Services.AddSingleton(jwtSettings);

// ============================
// Email Settings
// ============================
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, EmailService>();

// ============================
// JWT Authentication
// ============================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
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

    options.MapInboundClaims = false;
});

// ============================
// RBAC Authorization
// ============================
builder.Services.AddRbacAuthorization();

// ============================
// CORS
// ============================
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());

    options.AddPolicy("Production", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Cloudinary
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<IFileUploadService, CloudinaryFileUploadService>();

// ============================
// Health Checks
// ============================
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

// ============================
// Dependency Injection
// ============================
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
builder.Services.AddScoped<ITenantProvider, CurrentTenantProvider>();

// ============================
// Background Services
// ============================
builder.Services.AddHostedService<DashboardSnapshotService>();

var app = builder.Build();

// ============================
// Middleware Pipeline
// ============================
if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();

    app.UseCors("AllowAll");
}
else
{
    app.UseCors("Production");
    app.UseHttpsRedirection();
}

// ============================
// Health Check - All Environments
// ============================
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),

            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds + "ms"
            }),

            totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
        });

        await context.Response.WriteAsync(result);
    }
});

app.UseRateLimiter();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();

app.UseMiddleware<TenantMiddleware>();

app.UseAuthorization();

app.MapControllers();

// ============================
// Auto Migrate + Seed
// ============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}

await DbSeeder.SeedAsync(app.Services);

app.Run();