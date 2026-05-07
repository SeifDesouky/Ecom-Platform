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

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// NSwag
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

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Settings
var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()!;

builder.Services.AddSingleton(jwtSettings);

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
});

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

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

// Tenant Provider
builder.Services.AddScoped<ITenantProvider, CurrentTenantProvider>();

// Background Services
builder.Services.AddHostedService<DashboardSnapshotService>();

var app = builder.Build();

// ============================
// Middleware
// ============================

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();

// Tenant Middleware
app.UseMiddleware<TenantMiddleware>();

app.UseAuthorization();

app.MapControllers();

// ============================
// Auto Migrate
// ============================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}

app.Run();