using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces;
using EcomPlatform.Core.Interfaces.Repositories;
using EcomPlatform.Infrastructure.Data;

namespace EcomPlatform.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IRepository<Tenant> Tenants { get; }
        public IRepository<User> Users { get; }
        public IRepository<RefreshToken> RefreshTokens { get; }
        public IRepository<Category> Categories { get; }
        public IProductRepository Products { get; }
        public IRepository<ProductImage> ProductImages { get; }
        public IOrderRepository Orders { get; }
        public IRepository<OrderItem> OrderItems { get; }
        public IRepository<Customer> Customers { get; }
        public IRepository<CustomerAddress> CustomerAddresses { get; }
        public IRepository<Coupon> Coupons { get; }
        public IRepository<Plan> Plans { get; }
        public IRepository<Subscription> Subscriptions { get; }
        public IRepository<Ticket> Tickets { get; }
        public IRepository<TicketReply> TicketReplies { get; }
        public IRepository<ShippingZone> ShippingZones { get; }
        public IRepository<ShippingMethod> ShippingMethods { get; }
        public IRepository<Invoice> Invoices { get; }
        public IRepository<InvoiceItem> InvoiceItems { get; }
        public IRepository<Notification> Notifications { get; }
        public IRepository<Setting> Settings { get; }
        public IRepository<TenantDomain> TenantDomains { get; }
        public IRepository<Page> Pages { get; }
        public IRepository<Article> Articles { get; }
        public IRepository<AuditLog> AuditLogs { get; }
        public IRepository<DashboardSnapshot> DashboardSnapshots { get; }
        public IRepository<PasswordResetToken> PasswordResetTokens { get; }
        public IRepository<StoreIntegration> StoreIntegrations { get; }
        public IRepository<SyncLog> SyncLogs { get; }
        public IRepository<WebhookEvent> WebhookEvents { get; }
        public IRepository<UserProfile> UserProfiles { get; }  // ✅ جديد
        public IRepository<Warehouse> Warehouses { get; }
        public IRepository<StockMovement> StockMovements { get; }
        public IRepository<PaymentLink> PaymentLinks { get; }
        public IRepository<PaymentLinkItem> PaymentLinkItems { get; }
        public IRepository<PaymentLinkTransaction> PaymentLinkTransactions { get; }
        public IRepository<ReturnRequest> ReturnRequests { get; }
        public IRepository<ReturnItem> ReturnItems { get; }
        public IRepository<PosSession> PosSessions { get; }
        public IRepository<PosOrder>     PosOrders     { get; }
        public IRepository<PosOrderItem> PosOrderItems { get; }
        public IRepository<ProductReview> ProductReviews { get; }
        public IRepository<LoyaltyPoint> LoyaltyPoints { get; }
        public IRepository<MailingList> MailingLists { get; }
        public IRepository<MailingListSubscriber> MailingListSubscribers { get; }
        public IRepository<Campaign> Campaigns { get; }
        public IRepository<CampaignMailingList> CampaignMailingLists { get; }
        public IRepository<CampaignRecipient> CampaignRecipients { get; }
        public IRepository<ChartOfAccount> ChartOfAccounts { get; }
        public IRepository<JournalEntry> JournalEntries { get; }
        public IRepository<JournalEntryLine> JournalEntryLines { get; }
        public IRepository<HelpCategory> HelpCategories { get; }
        public IRepository<HelpArticle> HelpArticles { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Tenants = new Repository<Tenant>(context);
            Users = new Repository<User>(context);
            RefreshTokens = new Repository<RefreshToken>(context);
            Categories = new Repository<Category>(context);
            Products = new ProductRepository(context);
            ProductImages = new Repository<ProductImage>(context);
            Orders = new OrderRepository(context);
            OrderItems = new Repository<OrderItem>(context);
            Customers = new Repository<Customer>(context);
            CustomerAddresses = new Repository<CustomerAddress>(context);
            Coupons = new Repository<Coupon>(context);
            Plans = new Repository<Plan>(context);
            Subscriptions = new Repository<Subscription>(context);
            Tickets = new Repository<Ticket>(context);
            TicketReplies = new Repository<TicketReply>(context);
            ShippingZones = new Repository<ShippingZone>(context);
            ShippingMethods = new Repository<ShippingMethod>(context);
            Invoices = new Repository<Invoice>(context);
            InvoiceItems = new Repository<InvoiceItem>(context);
            Notifications = new Repository<Notification>(context);
            Settings = new Repository<Setting>(context);
            TenantDomains = new Repository<TenantDomain>(context);
            Pages = new Repository<Page>(context);
            Articles = new Repository<Article>(context);
            AuditLogs = new Repository<AuditLog>(context);
            DashboardSnapshots = new Repository<DashboardSnapshot>(context);
            PasswordResetTokens = new Repository<PasswordResetToken>(context);
            StoreIntegrations = new Repository<StoreIntegration>(context);
            SyncLogs = new Repository<SyncLog>(context);
            WebhookEvents = new Repository<WebhookEvent>(context);
            UserProfiles = new Repository<UserProfile>(context);  // ✅ جديد
            Warehouses = new Repository<Warehouse>(context);
            StockMovements = new Repository<StockMovement>(context);
            PaymentLinks = new Repository<PaymentLink>(context);
            PaymentLinkItems = new Repository<PaymentLinkItem>(context);
            PaymentLinkTransactions = new Repository<PaymentLinkTransaction>(context);
            ReturnRequests = new Repository<ReturnRequest>(context);
            ReturnItems = new Repository<ReturnItem>(context);
            PosSessions = new Repository<PosSession>(context);
            PosOrders     = new Repository<PosOrder>(context);
            PosOrderItems = new Repository<PosOrderItem>(context);
            ProductReviews = new Repository<ProductReview>(context);
            LoyaltyPoints = new Repository<LoyaltyPoint>(context);
            MailingLists = new Repository<MailingList>(context);
            MailingListSubscribers = new Repository<MailingListSubscriber>(context);
            Campaigns = new Repository<Campaign>(context);
            CampaignMailingLists = new Repository<CampaignMailingList>(context);
            CampaignRecipients = new Repository<CampaignRecipient>(context);
            ChartOfAccounts = new Repository<ChartOfAccount>(context);
            JournalEntries = new Repository<JournalEntry>(context);
            JournalEntryLines = new Repository<JournalEntryLine>(context);
            HelpCategories = new Repository<HelpCategory>(context);
            HelpArticles = new Repository<HelpArticle>(context);

        }

        public async Task<int> SaveChangesAsync() =>
            await _context.SaveChangesAsync();

        public void Dispose() => _context.Dispose();
    }
}