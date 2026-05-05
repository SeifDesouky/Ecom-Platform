using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Interfaces.Repositories;

namespace EcomPlatform.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Entities.Tenant> Tenants { get; }
        IRepository<Entities.User> Users { get; }
        IRepository<Category> Categories { get; }
        IRepository<Product> Products { get; }
        IRepository<ProductImage> ProductImages { get; }
        IRepository<Order> Orders { get; }
        IRepository<OrderItem> OrderItems { get; }
        IRepository<Customer> Customers { get; }
        IRepository<CustomerAddress> CustomerAddresses { get; }
        IRepository<Coupon> Coupons { get; }
        IRepository<Plan> Plans { get; }
        IRepository<Subscription> Subscriptions { get; }
        IRepository<Ticket> Tickets { get; }
        IRepository<TicketReply> TicketReplies { get; }



        Task<int> SaveChangesAsync();
    }
}