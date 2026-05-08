using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace EcomPlatform.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ITenantProvider _tenantProvider;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ITenantProvider tenantProvider)
            : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();   // ← الجديد
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketReply> TicketReplies => Set<TicketReply>();
        public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
        public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Setting> Settings => Set<Setting>();
        public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
        public DbSet<Page> Pages => Set<Page>();
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // ================================================================
            // Global Query Filters
            // القاعدة: كل entity بـ TenantId → filter بالـ TenantId + IsDeleted
            // كل entity بدون TenantId (global) → filter بالـ IsDeleted فقط
            // مهم: HasQueryFilter يُستدعى مرة واحدة بس لكل entity
            // ================================================================

            // --- Tenant-scoped entities (TenantId + IsDeleted) ---

            modelBuilder.Entity<Product>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Category>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Order>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Customer>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Coupon>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Invoice>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<ShippingZone>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Subscription>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<TenantDomain>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Notification>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Setting>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Page>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Article>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Ticket>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<AuditLog>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            // --- Global entities (IsDeleted فقط، مش مربوطة بـ Tenant) ---

            modelBuilder.Entity<User>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<Plan>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<Tenant>()
                .HasQueryFilter(x => !x.IsDeleted);

            // --- Child entities بدون TenantId مباشر ---

            modelBuilder.Entity<OrderItem>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<CustomerAddress>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ProductImage>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<InvoiceItem>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<TicketReply>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ShippingMethod>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<DashboardSnapshot>()
                .HasQueryFilter(x => !x.IsDeleted);

            // ← الجديد: RefreshToken مش بيحتاج tenant filter — هو user-scoped
            modelBuilder.Entity<RefreshToken>()
                .HasQueryFilter(x => !x.IsDeleted);
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }

                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}