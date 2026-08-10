using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Aggregates;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers", table =>
            table.HasCheckConstraint("CK_vouchers_sequence", "\"SequenceNumber\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SupplierName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ShopName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductTitle).HasMaxLength(300).IsRequired();
        builder.Property(x => x.RedeemedShopName).HasMaxLength(200);
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CodeCiphertext).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.RevocationReason).HasMaxLength(500);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.StoreOrderItemId, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => x.CodeHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IssuedAtUtc });
        builder.HasIndex(x => new { x.StoreOrderId, x.Status });
        builder.HasIndex(x => x.OrderId);
        builder.HasOne<StoreOrderItem>().WithMany().HasForeignKey(x => x.StoreOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoreOrder>().WithMany().HasForeignKey(x => x.StoreOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("voucher_redemptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.VendorUserId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.ShopId, x.RedeemedAtUtc });
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
