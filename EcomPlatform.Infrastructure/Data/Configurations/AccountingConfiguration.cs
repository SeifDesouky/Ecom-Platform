using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
    {
        public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
        {
            builder.ToTable("ChartOfAccounts");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.NameEn).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(500);

            // كود الحساب فريد لكل tenant
            builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            builder.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
    {
        public void Configure(EntityTypeBuilder<JournalEntry> builder)
        {
            builder.ToTable("JournalEntries");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.EntryNumber).IsRequired().HasMaxLength(40);
            builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Notes).HasMaxLength(1000);
            builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
            builder.Property(x => x.TotalDebit).HasPrecision(18, 2);
            builder.Property(x => x.TotalCredit).HasPrecision(18, 2);

            builder.HasIndex(x => x.EntryNumber).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Status, x.EntryDate });
            builder.HasIndex(x => new { x.ReferenceId, x.Source });

            builder.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
    {
        public void Configure(EntityTypeBuilder<JournalEntryLine> builder)
        {
            builder.ToTable("JournalEntryLines");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AccountCode).HasMaxLength(20);
            builder.Property(x => x.AccountName).HasMaxLength(200);
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Debit).HasPrecision(18, 2);
            builder.Property(x => x.Credit).HasPrecision(18, 2);

            builder.HasIndex(x => x.JournalEntryId);
            builder.HasIndex(x => x.AccountId);

            builder.HasOne(x => x.JournalEntry)
                .WithMany(e => e.Lines)
                .HasForeignKey(x => x.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Account)
                .WithMany(a => a.Lines)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
