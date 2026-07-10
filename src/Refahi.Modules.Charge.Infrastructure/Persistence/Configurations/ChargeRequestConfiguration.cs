using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Charge.Domain.Aggregates;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Configurations;

public sealed class ChargeRequestConfiguration : IEntityTypeConfiguration<ChargeRequest>
{
    public void Configure(EntityTypeBuilder<ChargeRequest> b)
    {
        b.ToTable("charge_requests");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("id");

        b.Property(x => x.SagaId)
            .HasColumnName("saga_id");

        b.Property(x => x.UserId)
            .HasColumnName("user_id");

        b.Property(x => x.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(50);


        b.Property(x => x.Operator)
            .HasColumnName("operator")
            .HasConversion<short>();

        b.Property(x => x.ServiceType)
            .HasColumnName("service_type")
            .HasConversion<short>();

        b.Property(x => x.DestinationMobileNumber)
            .HasColumnName("destination_mobile_number")
            .HasMaxLength(20);

        b.Property(x => x.OriginMobileNumber)
            .HasColumnName("origin_mobile_number")
            .HasMaxLength(20);

        b.Property(x => x.ProviderProductId)
            .HasColumnName("provider_product_id")
            .HasMaxLength(150);

        b.Property(x => x.ProductCaption)
            .HasColumnName("product_caption")
            .HasMaxLength(500);

        b.Property(x => x.ProductCategory)
            .HasColumnName("product_category");

        b.Property(x => x.PayBill)
            .HasColumnName("pay_bill");

        b.Property(x => x.PinCategoryId)
            .HasColumnName("pin_category_id");

        b.Property(x => x.PinCount)
            .HasColumnName("pin_count");

        b.Property(x => x.ProductSnapshotJson)
            .HasColumnName("product_snapshot_json").HasColumnType("jsonb");

        b.Property(x => x.ProviderCostMinor)
            .HasColumnName("provider_cost_minor");

        b.Property(x => x.MarkupRuleId)
            .HasColumnName("markup_rule_id");

        b.Property(x => x.MarkupPercent)
            .HasColumnName("markup_percent").HasPrecision(9, 4);

        b.Property(x => x.MarkupFixedMinor)
            .HasColumnName("markup_fixed_minor");

        b.Property(x => x.MarkupAmountMinor)
            .HasColumnName("markup_amount_minor");

        b.Property(x => x.FinalAmountMinor)
            .HasColumnName("final_amount_minor");

        b.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3);

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<short>();

        b.Property(x => x.OrderId)
            .HasColumnName("order_id");

        b.Property(x => x.PaymentId)
            .HasColumnName("payment_id");

        b.Property(x => x.CustomerInvoiceNumber)
            .HasColumnName("customer_invoice_number")
            .HasMaxLength(80);

        b.Property(x => x.ProviderRrn)
            .HasColumnName("provider_rrn").HasMaxLength(100);

        b.Property(x => x.ProviderTraceId)
            .HasColumnName("provider_trace_id")
            .HasMaxLength(150);

        b.Property(x => x.EniacResultCode)
            .HasColumnName("eniac_result_code");

        b.Property(x => x.OperatorResultCode)
            .HasColumnName("operator_result_code")
            .HasMaxLength(50);

        b.Property(x => x.ProviderMessage)
            .HasColumnName("provider_message")
            .HasMaxLength(2000);

        b.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200);

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        b.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        b.Property(x => x.ExpireAt)
            .HasColumnName("expire_at");

        b.Property(x => x.PaidAt)
            .HasColumnName("paid_at");

        b.Property(x => x.FulfilledAt)
            .HasColumnName("fulfilled_at");

        b.Property(x => x.NextReconciliationAt)
            .HasColumnName("next_reconciliation_at");

        b.Property(x => x.ReconciliationCount)
            .HasColumnName("reconciliation_count");

        b.Property(x => x.ProcessingLeaseUntil)
            .HasColumnName("processing_lease_until");

        b.Property(x => x.ProcessingLeaseOwner)
            .HasColumnName("processing_lease_owner").HasMaxLength(100);

        b.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken()
            .IsRowVersion();


        b.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_charge_requests_user_idempotency");

        b.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("order_id IS NOT NULL")
            .HasDatabaseName("ux_charge_requests_order_id");

        b.HasIndex(x => x.CustomerInvoiceNumber)
            .IsUnique()
            .HasDatabaseName("ux_charge_requests_invoice");

        b.HasIndex(x => new { x.Status, x.NextReconciliationAt })
            .HasDatabaseName("ix_charge_requests_work");

        b.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(x => x.ChargeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Pins)
            .WithOne()
            .HasForeignKey(x => x.ChargeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Metadata
            .FindNavigation(nameof(ChargeRequest.Attempts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        b.Metadata
            .FindNavigation(nameof(ChargeRequest.Pins))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
