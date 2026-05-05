using EcomPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcomPlatform.Infrastructure.Data.Configurations
{
    public class TicketReplyConfiguration : IEntityTypeConfiguration<TicketReply>
    {
        public void Configure(EntityTypeBuilder<TicketReply> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Message)
                .IsRequired();

            builder.HasOne(t => t.Ticket)
                .WithMany(t => t.Replies)
                .HasForeignKey(t => t.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(t => !t.IsDeleted);
        }
    }
}