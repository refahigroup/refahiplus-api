using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Configurations;

public sealed class VoucherRefundOverrideConfiguration
    : IEntityTypeConfiguration<VoucherRefundOverride>
{
    public void Configure(EntityTypeBuilder<VoucherRefundOverride> builder)
    {
        builder.ToTable("voucher_refund_overrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.VoucherSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.StoreOrderId).IsUnique();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.CorrelationId).IsUnique();
        builder
            .HasOne<StoreOrder>()
            .WithMany()
            .HasForeignKey(x => x.StoreOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VoucherRefundOverrideAttemptConfiguration
    : IEntityTypeConfiguration<VoucherRefundOverrideAttempt>
{
    public void Configure(EntityTypeBuilder<VoucherRefundOverrideAttempt> builder)
    {
        builder.ToTable("voucher_refund_override_attempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PaymentAction).HasMaxLength(32);
        builder.Property(x => x.FailureCode).HasMaxLength(128);
        builder.Property(x => x.FailureMessage).HasMaxLength(500);
        builder.HasIndex(x => new { x.VoucherRefundOverrideId, x.SequenceNumber }).IsUnique();
        builder
            .HasOne<VoucherRefundOverride>()
            .WithMany()
            .HasForeignKey(x => x.VoucherRefundOverrideId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
