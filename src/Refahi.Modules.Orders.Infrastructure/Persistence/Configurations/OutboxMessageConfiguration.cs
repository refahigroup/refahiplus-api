using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Orders.Infrastructure.Outbox;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.EventType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("event_type");

        builder.Property(o => o.EventData)
            .IsRequired()
            .HasColumnName("event_data");

        builder.Property(o => o.OccurredAt)
            .IsRequired()
            .HasColumnName("occurred_at");

        builder.Property(o => o.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(o => o.Error)
            .HasMaxLength(2000)
            .HasColumnName("error");

        builder.HasIndex(o => o.ProcessedAt)
            .HasDatabaseName("ix_outbox_messages_processed_at");
    }
}
