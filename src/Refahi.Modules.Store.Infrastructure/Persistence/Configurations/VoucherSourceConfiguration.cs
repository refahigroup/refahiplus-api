using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class VoucherSourceConfiguration : IEntityTypeConfiguration<VoucherSource>
{
    public void Configure(EntityTypeBuilder<VoucherSource> builder)
    {
        builder.ToTable("voucher_sources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceType).HasConversion<short>();
        builder.Property(x => x.RedemptionMode).HasConversion<short>();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.SupplierId, x.Title }).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.IsActive });
    }
}

public sealed class VoucherSourceCodeConfiguration : IEntityTypeConfiguration<VoucherSourceCode>
{
    public void Configure(EntityTypeBuilder<VoucherSourceCode> builder)
    {
        builder.ToTable("voucher_source_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CodeCiphertext).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.SupplierId, x.CodeHash }).IsUnique();
        builder.HasIndex(x => new { x.VoucherSourceId, x.Status, x.ExpiresAtUtc, x.RegisteredAtUtc });
        builder.HasOne<VoucherSource>().WithMany().HasForeignKey(x => x.VoucherSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VoucherCodeAllocationConfiguration : IEntityTypeConfiguration<VoucherCodeAllocation>
{
    public void Configure(EntityTypeBuilder<VoucherCodeAllocation> builder)
    {
        builder.ToTable("voucher_code_allocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.StoreOrderItemId, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => x.VoucherSourceCodeId)
            .IsUnique()
            .HasFilter("\"Status\" IN (1, 2)");
        builder.HasIndex(x => x.VoucherId).IsUnique().HasFilter("\"VoucherId\" IS NOT NULL");
        builder.HasIndex(x => new { x.Status, x.ReservedUntilUtc });
        builder.HasOne<VoucherSourceCode>().WithMany().HasForeignKey(x => x.VoucherSourceCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoreOrder>().WithMany().HasForeignKey(x => x.StoreOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StoreOrderItem>().WithMany().HasForeignKey(x => x.StoreOrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VoucherCodeImportBatchConfiguration : IEntityTypeConfiguration<VoucherCodeImportBatch>
{
    public void Configure(EntityTypeBuilder<VoucherCodeImportBatch> builder)
    {
        builder.ToTable("voucher_code_import_batches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.VoucherSourceId, x.IdempotencyKey }).IsUnique();
        builder.HasOne<VoucherSource>().WithMany().HasForeignKey(x => x.VoucherSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VoucherDeliveryConfiguration : IEntityTypeConfiguration<VoucherDelivery>
{
    public void Configure(EntityTypeBuilder<VoucherDelivery> builder)
    {
        builder.ToTable("voucher_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<short>();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasIndex(x => new { x.VoucherId, x.Channel }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
