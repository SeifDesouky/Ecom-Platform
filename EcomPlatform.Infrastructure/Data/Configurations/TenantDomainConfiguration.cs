using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
    {
        public void Configure(EntityTypeBuilder<TenantDomain> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.Domain)
                .IsRequired()
                .HasMaxLength(200);

            builder.HasIndex(d => d.Domain).IsUnique();

            builder.Property(d => d.VerificationToken)
                .HasMaxLength(100);

            builder.Property(d => d.Notes)
                .HasMaxLength(500);

            builder.HasOne(d => d.Tenant)
                .WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(d => !d.IsDeleted);
        }
    }
}