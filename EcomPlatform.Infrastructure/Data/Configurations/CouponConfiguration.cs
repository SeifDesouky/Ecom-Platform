using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
    {
        public void Configure(EntityTypeBuilder<Coupon> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(c => new { c.Code, c.TenantId }).IsUnique();

            builder.Property(c => c.Description)
                .HasMaxLength(200);

            builder.Property(c => c.Value)
                .HasColumnType("decimal(18,2)");

            builder.Property(c => c.MinOrderAmount)
                .HasColumnType("decimal(18,2)");

            builder.Property(c => c.MaxDiscountAmount)
                .HasColumnType("decimal(18,2)");

            builder.HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(c => !c.IsDeleted);
        }
    }
}