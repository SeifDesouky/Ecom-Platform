using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(t => t.Slug)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(t => t.Slug).IsUnique();
            builder.HasIndex(t => t.Email).IsUnique();

            builder.Property(t => t.Email)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(t => t.Domain)
                .HasMaxLength(200);

            builder.HasQueryFilter(t => !t.IsDeleted);
        }
    }
}