using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Orders.Domain.Entities;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderPaymentPostingConfiguration : IEntityTypeConfiguration<OrderPaymentPosting>
{
    public void Configure(EntityTypeBuilder<OrderPaymentPosting> builder)
    {
        builder.ToTable("order_payment_postings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
        builder
            .Property(x => x.Direction)
            .HasColumnName("direction")
            .HasColumnType("smallint")
            .IsRequired();
        builder.Property(x => x.AmountMinor).HasColumnName("amount_minor").IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(80).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.HasIndex(x => new { x.OrderId, x.SortOrder }).IsUnique();
    }
}
