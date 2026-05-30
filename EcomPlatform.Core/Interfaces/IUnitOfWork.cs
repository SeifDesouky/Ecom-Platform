using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces.Repositories;

namespace EcomPlatform.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Entities.Tenant> Tenants { get; }
        IRepository<Entities.User> Users { get; }
        IRepository<RefreshToken> RefreshTokens { get; }
        IRepository<Category> Categories { get; }
        IProductRepository Products { get; }
        IRepository<ProductImage> ProductImages { get; }
        IOrderRepository Orders { get; }
        IRepository<OrderItem> OrderItems { get; }
        IRepository<Customer> Customers { get; }
        IRepository<CustomerAddress> CustomerAddresses { get; }
        IRepository<Coupon> Coupons { get; }
        IRepository<Plan> Plans { get; }
        IRepository<Subscription> Subscriptions { get; }
        IRepository<Ticket> Tickets { get; }
        IRepository<TicketReply> TicketReplies { get; }
        IRepository<ShippingZone> ShippingZones { get; }
        IRepository<ShippingMethod> ShippingMethods { get; }
        IRepository<Invoice> Invoices { get; }
        IRepository<InvoiceItem> InvoiceItems { get; }
        IRepository<Notification> Notifications { get; }
        IRepository<Setting> Settings { get; }
        IRepository<TenantDomain> TenantDomains { get; }
        IRepository<Page> Pages { get; }
        IRepository<Article> Articles { get; }
        IRepository<AuditLog> AuditLogs { get; }
        IRepository<StoreIntegration> StoreIntegrations { get; }
        IRepository<SyncLog> SyncLogs { get; }
        IRepository<WebhookEvent> WebhookEvents { get; }
        IRepository<DashboardSnapshot> DashboardSnapshots { get; }
        IRepository<PasswordResetToken> PasswordResetTokens { get; }
        IRepository<UserProfile> UserProfiles { get; }
        IRepository<Warehouse> Warehouses { get; }
        IRepository<StockMovement> StockMovements { get; }
        IRepository<PaymentLink> PaymentLinks { get; }
        IRepository<PaymentLinkItem> PaymentLinkItems { get; }
        IRepository<PaymentLinkTransaction> PaymentLinkTransactions { get; }
        IRepository<ReturnRequest> ReturnRequests { get; }
        IRepository<ReturnItem> ReturnItems { get; }
        IRepository<PosSession> PosSessions { get; }
        IRepository<PosOrder> PosOrders { get; }
        IRepository<PosOrderItem> PosOrderItems { get; }
        IRepository<ProductReview> ProductReviews { get; }
        IRepository<LoyaltyPoint> LoyaltyPoints { get; }
        IRepository<MailingList> MailingLists { get; }
        IRepository<MailingListSubscriber> MailingListSubscribers { get; }
        IRepository<Campaign> Campaigns { get; }
        IRepository<CampaignMailingList> CampaignMailingLists { get; }
        IRepository<CampaignRecipient> CampaignRecipients { get; }
        IRepository<ChartOfAccount> ChartOfAccounts { get; }
        IRepository<JournalEntry> JournalEntries { get; }
        IRepository<JournalEntryLine> JournalEntryLines { get; }
        IRepository<HelpCategory> HelpCategories { get; }
        IRepository<HelpArticle> HelpArticles { get; }
        Task<int> SaveChangesAsync();
    }
}