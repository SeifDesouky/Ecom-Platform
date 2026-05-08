using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(p => p.Slug)
                .IsRequired()
                .HasMaxLength(200);

            builder.HasIndex(p => new { p.Slug, p.TenantId }).IsUnique();
            builder.HasIndex(p => p.TenantId);
            builder.HasIndex(p => new { p.TenantId, p.CategoryId });
            builder.HasIndex(p => new { p.TenantId, p.IsActive });
            builder.HasIndex(p => new { p.TenantId, p.Status });

            builder.Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.ComparePrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.CostPrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.Weight)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.SKU)
                .HasMaxLength(100);

            builder.HasOne(p => p.Tenant)
                .WithMany(t => t.Products)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(p => !p.IsDeleted);
        }
    }
}