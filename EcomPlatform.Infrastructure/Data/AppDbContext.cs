using EcomPlatform.Application.Common.Interfaces;
using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Entities.Common;
using EcomPlatform.Infrastructure.Data.Configurations;
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
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

        // ── Marketplace Integrations ──────────────────────────────────────────
        public DbSet<StoreIntegration> StoreIntegrations => Set<StoreIntegration>();
        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
        public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<PaymentLink> PaymentLinks { get; set; }
        public DbSet<PaymentLinkItem> PaymentLinkItems { get; set; }
        public DbSet<PaymentLinkTransaction> PaymentLinkTransactions { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<ReturnItem> ReturnItems { get; set; }
        public DbSet<PosSession> PosSessions => Set<PosSession>();
        public DbSet<PosOrder> PosOrders => Set<PosOrder>();
        public DbSet<PosOrderItem> PosOrderItems => Set<PosOrderItem>();
        public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
        public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
        public DbSet<MailingList> MailingLists => Set<MailingList>();
        public DbSet<MailingListSubscriber> MailingListSubscribers => Set<MailingListSubscriber>();
        public DbSet<Campaign> Campaigns => Set<Campaign>();
        public DbSet<CampaignMailingList> CampaignMailingLists => Set<CampaignMailingList>();
        public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }

        // ── Help Center ───────────────────────────────────────────────────────
        public DbSet<HelpCategory> HelpCategories => Set<HelpCategory>();
        public DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();


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

            // ── POS ──────────────────────────────────────────────────────────
            modelBuilder.Entity<PosSession>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<PosOrder>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<ProductReview>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            // ── Loyalty ──────────────────────────────────────────────────────
            modelBuilder.Entity<LoyaltyPoint>()
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

            modelBuilder.Entity<PaymentLinkItem>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<PosOrderItem>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ReturnItem>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<ReturnRequest>()
                .HasQueryFilter(x => !x.IsDeleted);

            // ── Warehouse & StockMovement ─────────────────────────────────────
            modelBuilder.Entity<Warehouse>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<StockMovement>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<DashboardSnapshot>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<RefreshToken>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<PasswordResetToken>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<UserProfile>()
                .HasQueryFilter(x => !x.IsDeleted && !x.User.IsDeleted);

            // ── Mailing & Campaigns ───────────────────────────────────────────
            modelBuilder.Entity<MailingList>()
                .HasQueryFilter(x => !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue || x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<MailingListSubscriber>()
                .HasQueryFilter(x => !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue || x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<Campaign>()
                .HasQueryFilter(x => !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue || x.TenantId == _tenantProvider.TenantId));

            // ✅ Fix: matching filters for Campaign children
            modelBuilder.Entity<CampaignMailingList>()
                .HasQueryFilter(x => !x.IsDeleted);

            modelBuilder.Entity<CampaignRecipient>()
                .HasQueryFilter(x => !x.IsDeleted);

            // ── Help Center ───────────────────────────────────────────────────
            modelBuilder.Entity<HelpCategory>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.Entity<HelpArticle>()
                .HasQueryFilter(x =>
                    !x.IsDeleted &&
                    (!_tenantProvider.TenantId.HasValue ||
                     x.TenantId == _tenantProvider.TenantId));

            modelBuilder.ApplyConfiguration(new PaymentLinkConfiguration());
            modelBuilder.ApplyConfiguration(new PaymentLinkItemConfiguration());
            modelBuilder.ApplyConfiguration(new PaymentLinkTransactionConfiguration());
            modelBuilder.ApplyConfiguration(new ReturnRequestConfiguration());
            modelBuilder.ApplyConfiguration(new ReturnItemConfiguration());
            modelBuilder.ApplyConfiguration(new ChartOfAccountConfiguration());
            modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
            modelBuilder.ApplyConfiguration(new JournalEntryLineConfiguration());
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