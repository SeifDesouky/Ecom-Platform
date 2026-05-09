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
        public IRepository<RefreshToken> RefreshTokens { get; }   // ← الجديد
        public IRepository<Category> Categories { get; }
        public IRepository<Product> Products { get; }
        public IRepository<ProductImage> ProductImages { get; }
        public IRepository<Order> Orders { get; }
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

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Tenants = new Repository<Tenant>(context);
            Users = new Repository<User>(context);
            RefreshTokens = new Repository<RefreshToken>(context);   // ← الجديد
            Categories = new Repository<Category>(context);
            Products = new Repository<Product>(context);
            ProductImages = new Repository<ProductImage>(context);
            Orders = new Repository<Order>(context);
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

        }

        public async Task<int> SaveChangesAsync() =>
            await _context.SaveChangesAsync();

        public void Dispose() => _context.Dispose();
    }
}