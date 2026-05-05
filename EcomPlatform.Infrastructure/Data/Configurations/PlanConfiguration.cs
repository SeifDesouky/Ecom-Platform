using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Description)
                .HasMaxLength(500);

            builder.Property(p => p.MonthlyPrice)
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.YearlyPrice)
                .HasColumnType("decimal(18,2)");

            builder.HasQueryFilter(p => !p.IsDeleted);
        }
    }
}