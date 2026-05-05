using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.OrderNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(o => o.OrderNumber).IsUnique();

            builder.Property(o => o.SubTotal)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.ShippingCost)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.Discount)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.Tax)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.Total)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.CustomerName)
                .HasMaxLength(100);

            builder.Property(o => o.CustomerEmail)
                .HasMaxLength(150);

            builder.Property(o => o.CustomerPhone)
                .HasMaxLength(20);

            builder.Property(o => o.ShippingCity)
                .HasMaxLength(100);

            builder.Property(o => o.ShippingCountry)
                .HasMaxLength(100);

            builder.HasOne(o => o.Tenant)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(o => !o.IsDeleted);
        }
    }
}