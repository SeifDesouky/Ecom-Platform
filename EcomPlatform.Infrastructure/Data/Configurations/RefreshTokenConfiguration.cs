// ================================================================
// EcomPlatform.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs
// ================================================================
using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);  // SHA-256 hex string = 64 chars، بنحط 128 هامش

            builder.Property(x => x.DeviceInfo)
                .HasMaxLength(512);

            builder.Property(x => x.IpAddress)
                .HasMaxLength(45);   // IPv6 max length

            builder.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(128);

            // Index على TokenHash لأن الـ lookup بيحصل عليه
            builder.HasIndex(x => x.TokenHash)
                .IsUnique();

            // Index على UserId لـ "Logout from all devices"
            builder.HasIndex(x => x.UserId);

            // Index مركب: UserId + IsRevoked + ExpiresAt لـ active tokens query
            builder.HasIndex(x => new { x.UserId, x.IsRevoked, x.ExpiresAt });

            // FK → User
            builder.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
