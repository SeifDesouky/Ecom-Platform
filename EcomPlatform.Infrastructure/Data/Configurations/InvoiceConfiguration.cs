using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.HasKey(i => i.Id);

            builder.Property(i => i.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(i => i.InvoiceNumber).IsUnique();

            builder.Property(i => i.SubTotal)
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.Tax)
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.Discount)
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.Total)
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.CustomerName)
                .HasMaxLength(100);

            builder.Property(i => i.CustomerEmail)
                .HasMaxLength(150);

            builder.Property(i => i.CustomerPhone)
                .HasMaxLength(20);

            builder.HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Order)
                .WithMany()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(i => !i.IsDeleted);
        }
    }
}