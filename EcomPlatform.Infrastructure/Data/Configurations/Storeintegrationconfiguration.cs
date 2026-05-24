using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class StoreIntegrationConfiguration : IEntityTypeConfiguration<StoreIntegration>
    {
        public void Configure(EntityTypeBuilder<StoreIntegration> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ApiKey)
                .HasMaxLength(500);

            builder.Property(x => x.ApiSecret)
                .HasMaxLength(500);

            builder.Property(x => x.RefreshToken)
                .HasMaxLength(1000);

            builder.Property(x => x.StoreUrl)
                .HasMaxLength(300);

            builder.Property(x => x.ExternalStoreId)
                .HasMaxLength(100);

            builder.Property(x => x.WebhookSecret)
                .HasMaxLength(500);

            builder.Property(x => x.LastErrorMessage)
                .HasMaxLength(1000);

            // ── Indexes ──────────────────────────────────────────────────────

            builder.HasIndex(x => x.TenantId);

            builder.HasIndex(x => new { x.TenantId, x.Platform })
                .HasDatabaseName("IX_StoreIntegrations_TenantId_Platform");

            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("IX_StoreIntegrations_TenantId_Status");

            // ── Relations ────────────────────────────────────────────────────

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.SyncLogs)
                .WithOne(x => x.StoreIntegration)
                .HasForeignKey(x => x.StoreIntegrationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.WebhookEvents)
                .WithOne(x => x.StoreIntegration)
                .HasForeignKey(x => x.StoreIntegrationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}