using EcomPlatform.Core.Entities;
using EcomPlatform.Core.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace EcomPlatform.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<User> Users => Set<User>();
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = DateTime.UtcNow;

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