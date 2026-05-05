using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.UnitPrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.TotalPrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(o => o.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(o => o.ProductSKU)
                .HasMaxLength(100);

            builder.Property(o => o.ProductImage)
                .HasMaxLength(500);

            builder.HasOne(o => o.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(o => o.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(o => o.Product)
                .WithMany()
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(o => !o.IsDeleted);
        }
    }
}