using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
    {
        public void Configure(EntityTypeBuilder<WebhookEvent> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.SourceIp)
                .HasMaxLength(45);

            builder.Property(x => x.Signature)
                .HasMaxLength(500);

            builder.Property(x => x.ExternalEntityId)
                .HasMaxLength(100);

            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(2000);

            // RawPayload — longtext (no max length)

            // ── Indexes ──────────────────────────────────────────────────────

            builder.HasIndex(x => x.StoreIntegrationId);

            builder.HasIndex(x => x.TenantId);

            builder.HasIndex(x => new { x.StoreIntegrationId, x.Status })
                .HasDatabaseName("IX_WebhookEvents_IntegrationId_Status");

            builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
                .HasDatabaseName("IX_WebhookEvents_TenantId_CreatedAt");

            builder.HasIndex(x => new { x.StoreIntegrationId, x.EventType })
                .HasDatabaseName("IX_WebhookEvents_IntegrationId_EventType");

            // ── Relations ────────────────────────────────────────────────────

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}