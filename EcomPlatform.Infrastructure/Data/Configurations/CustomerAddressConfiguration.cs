using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
    {
        public void Configure(EntityTypeBuilder<CustomerAddress> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(c => c.FullName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.Phone)
                .HasMaxLength(20);

            builder.Property(c => c.Address)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(c => c.City)
                .HasMaxLength(100);

            builder.Property(c => c.Country)
                .HasMaxLength(100);

            builder.Property(c => c.PostalCode)
                .HasMaxLength(20);

            builder.HasOne(c => c.Customer)
                .WithMany(c => c.Addresses)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(c => !c.IsDeleted);
        }
    }
}