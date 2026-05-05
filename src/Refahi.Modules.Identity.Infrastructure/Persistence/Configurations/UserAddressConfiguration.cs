using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("user_addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(a => a.Title)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnName("title");

        builder.Property(a => a.ProvinceId)
            .IsRequired()
            .HasColumnName("province_id");

        builder.Property(a => a.CityId)
            .IsRequired()
            .HasColumnName("city_id");

        builder.Property(a => a.FullAddress)
            .HasMaxLength(1000)
            .IsRequired()
            .HasColumnName("full_address");

        builder.Property(a => a.PostalCode)
            .HasMaxLength(10)
            .IsRequired()
            .HasColumnName("postal_code");

        builder.Property(a => a.ReceiverName)
            .HasMaxLength(200)
            .IsRequired()
            .HasColumnName("receiver_name");

        builder.Property(a => a.ReceiverPhone)
            .HasMaxLength(20)
            .IsRequired()
            .HasColumnName("receiver_phone");

        builder.Property(a => a.Plate)
            .HasMaxLength(20)
            .HasColumnName("plate");

        builder.Property(a => a.Unit)
            .HasMaxLength(20)
            .HasColumnName("unit");

        builder.Property(a => a.Latitude)
            .HasColumnName("latitude");

        builder.Property(a => a.Longitude)
            .HasColumnName("longitude");

        builder.Property(a => a.IsDefault)
            .IsRequired()
            .HasColumnName("is_default");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        // Foreign Key to User aggregate (without exposing navigation in User)
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_user_addresses_user_id");

        // فقط یک آدرس پیش‌فرض برای هر کاربر — Partial unique index
        builder.HasIndex(a => new { a.UserId, a.IsDefault })
            .IsUnique()
            .HasFilter("is_default = TRUE")
            .HasDatabaseName("ux_user_addresses_user_default");
    }
}
