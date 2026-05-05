using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
    {
        public void Configure(EntityTypeBuilder<ProductImage> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Url)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(p => p.Alt)
                .HasMaxLength(200);

            builder.HasOne(p => p.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(p => !p.IsDeleted);
        }
    }
}