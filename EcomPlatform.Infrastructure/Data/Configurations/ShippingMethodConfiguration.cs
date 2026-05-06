using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
    {
        public void Configure(EntityTypeBuilder<ShippingMethod> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.Cost)
                .HasColumnType("decimal(18,2)");

            builder.Property(s => s.MinOrderAmount)
                .HasColumnType("decimal(18,2)");

            builder.Property(s => s.MaxOrderAmount)
                .HasColumnType("decimal(18,2)");

            builder.HasOne(s => s.ShippingZone)
                .WithMany(z => z.Methods)
                .HasForeignKey(s => s.ShippingZoneId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(s => !s.IsDeleted);
        }
    }
}