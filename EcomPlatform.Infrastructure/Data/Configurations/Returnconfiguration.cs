using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
    {
        public void Configure(EntityTypeBuilder<ReturnRequest> builder)
        {
            builder.ToTable("ReturnRequests");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ReturnNumber).IsRequired().HasMaxLength(40);
            builder.Property(x => x.ReasonNote).HasMaxLength(1000);
            builder.Property(x => x.RefundNote).HasMaxLength(500);
            builder.Property(x => x.RefundGatewayTransactionId).HasMaxLength(200);
            builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
            builder.Property(x => x.ApprovedAmount).HasPrecision(18, 2);

            builder.HasIndex(x => x.ReturnNumber).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            builder.HasIndex(x => x.OrderId);

            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
    {
        public void Configure(EntityTypeBuilder<ReturnItem> builder)
        {
            builder.ToTable("ReturnItems");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductName).HasMaxLength(300);
            builder.Property(x => x.ProductSKU).HasMaxLength(100);
            builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

            builder.HasOne(x => x.ReturnRequest)
                .WithMany(r => r.Items)
                .HasForeignKey(x => x.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.OrderItem)
                .WithMany()
                .HasForeignKey(x => x.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}