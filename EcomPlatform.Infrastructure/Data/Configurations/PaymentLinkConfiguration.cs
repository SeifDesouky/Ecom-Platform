using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class PaymentLinkConfiguration : IEntityTypeConfiguration<PaymentLink>
    {
        public void Configure(EntityTypeBuilder<PaymentLink> builder)
        {
            builder.ToTable("PaymentLinks");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("SAR");
            builder.Property(x => x.SuccessRedirectUrl).HasMaxLength(500);
            builder.Property(x => x.FailureRedirectUrl).HasMaxLength(500);
            builder.Property(x => x.Metadata).HasMaxLength(2000);

            // Unique code per platform (not per tenant — الكود فريد globally)
            builder.HasIndex(x => x.Code).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });

            builder.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class PaymentLinkItemConfiguration : IEntityTypeConfiguration<PaymentLinkItem>
    {
        public void Configure(EntityTypeBuilder<PaymentLinkItem> builder)
        {
            builder.ToTable("PaymentLinkItems");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductName).HasMaxLength(300);
            builder.Property(x => x.UnitPrice).HasPrecision(18, 2);

            builder.HasOne(x => x.PaymentLink)
                .WithMany(l => l.Items)
                .HasForeignKey(x => x.PaymentLinkId)
                .OnDelete(DeleteBehavior.Cascade);   // حذف الرابط يحذف المنتجات

            builder.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class PaymentLinkTransactionConfiguration : IEntityTypeConfiguration<PaymentLinkTransaction>
    {
        public void Configure(EntityTypeBuilder<PaymentLinkTransaction> builder)
        {
            builder.ToTable("PaymentLinkTransactions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PayerName).HasMaxLength(200);
            builder.Property(x => x.PayerEmail).HasMaxLength(200);
            builder.Property(x => x.PayerPhone).HasMaxLength(30);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("SAR");
            builder.Property(x => x.GatewayName).HasMaxLength(100);
            builder.Property(x => x.GatewayTransactionId).HasMaxLength(200);
            builder.Property(x => x.GatewayResponse).HasMaxLength(4000);
            builder.Property(x => x.FailureReason).HasMaxLength(500);

            builder.HasOne(x => x.PaymentLink)
                .WithMany(l => l.Transactions)
                .HasForeignKey(x => x.PaymentLinkId)
                .OnDelete(DeleteBehavior.Restrict);  // لا تحذف الـ transactions لو حذفت الرابط

            builder.HasOne(x => x.GeneratedOrder)
                .WithMany()
                .HasForeignKey(x => x.GeneratedOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes للأداء
            builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            builder.HasIndex(x => x.GatewayTransactionId);
        }
    }
}
