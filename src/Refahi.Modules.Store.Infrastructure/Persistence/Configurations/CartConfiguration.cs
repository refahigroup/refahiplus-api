using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("carts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ModuleId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.Ignore(c => c.TotalMinor);

        // Each user has one cart per module
        builder.HasIndex(c => new { c.UserId, c.ModuleId }).IsUnique();

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey("CartId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
