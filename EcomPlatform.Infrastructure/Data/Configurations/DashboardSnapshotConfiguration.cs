using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class DashboardSnapshotConfiguration : IEntityTypeConfiguration<DashboardSnapshot>
    {
        public void Configure(EntityTypeBuilder<DashboardSnapshot> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.TotalRevenue)
                .HasColumnType("decimal(18,2)");

            builder.Property(d => d.RevenueThisMonth)
                .HasColumnType("decimal(18,2)");

            builder.HasOne(d => d.Tenant)
                .WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(d => !d.IsDeleted);
        }
    }
}