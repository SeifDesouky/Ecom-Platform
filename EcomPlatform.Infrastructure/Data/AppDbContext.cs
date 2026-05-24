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
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
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
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

        // ── Marketplace Integrations ──────────────────────────────────────────
        public DbSet<StoreIntegration> StoreIntegrations => Set<StoreIntegration>();
        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
        public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // ================================================================
            // Precision Configuration
            // ================================================================
            modelBuilder.Entity<Tenant>()
                .Property(x => x.VatRate)
                .HasPrecision(5, 2);

            // ================================================================
            // Global Query Filters
            // ================================================================

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

            // ── Marketplace Integrations ──────────────────────────────────────
            modelBuilder.Entity<StoreIntegration>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<SyncLog>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<WebhookEvent>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            // ================================================================
            // Global entities (IsDeleted only)
            // ================================================================

            modelBuilder.Entity<User>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<Plan>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<Tenant>()
                .HasQueryFilter(x => !x.IsDeleted);

            // ================================================================
            // Child / system entities
            // ================================================================

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

            modelBuilder.Entity<RefreshToken>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<PasswordResetToken>()
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