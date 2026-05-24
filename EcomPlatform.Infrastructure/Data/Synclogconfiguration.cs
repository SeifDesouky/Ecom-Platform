using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
    {
        public void Configure(EntityTypeBuilder<SyncLog> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            // ── Indexes ──────────────────────────────────────────────────────

            builder.HasIndex(x => x.StoreIntegrationId);

            builder.HasIndex(x => x.TenantId);

            builder.HasIndex(x => new { x.StoreIntegrationId, x.Status })
                .HasDatabaseName("IX_SyncLogs_IntegrationId_Status");

            builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
                .HasDatabaseName("IX_SyncLogs_TenantId_CreatedAt");

            // ── Relations ────────────────────────────────────────────────────

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}