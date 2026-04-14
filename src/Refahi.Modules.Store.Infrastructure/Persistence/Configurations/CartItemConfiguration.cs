using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.CartId).IsRequired();
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.VariantId);
        builder.Property(i => i.SessionId);     // v1.1 — nullable FK to product_sessions
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPriceMinor).IsRequired();

        builder.HasIndex(i => i.CartId);
        builder.HasIndex(i => i.ProductId);
    }
}
