using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Charge.Domain.Aggregates;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Configurations;

public sealed class ProviderCallLogConfiguration : IEntityTypeConfiguration<ProviderCallLog>
{
    public void Configure(EntityTypeBuilder<ProviderCallLog> b)
    {
        b.ToTable("provider_call_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ChargeRequestId).HasColumnName("charge_request_id");
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.SagaId).HasColumnName("saga_id");
        b.Property(x => x.ProviderName).HasColumnName("provider_name").HasMaxLength(50);
        b.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(80);
        b.Property(x => x.Stage).HasColumnName("stage").HasMaxLength(50);
        b.Property(x => x.Outcome).HasColumnName("outcome").HasConversion<short>();
        b.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(10);
        b.Property(x => x.Endpoint).HasColumnName("endpoint").HasMaxLength(500);
        b.Property(x => x.HttpStatusCode).HasColumnName("http_status_code");
        b.Property(x => x.ProviderResultCode).HasColumnName("provider_result_code");
        b.Property(x => x.OperatorResultCode).HasColumnName("operator_result_code").HasMaxLength(50);
        b.Property(x => x.Retryable).HasColumnName("retryable");
        b.Property(x => x.AttemptNumber).HasColumnName("attempt_number");
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        b.Property(x => x.ExceptionType).HasColumnName("exception_type").HasMaxLength(300);
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        b.Property(x => x.RequestSnapshotJson).HasColumnName("request_snapshot_json").HasColumnType("jsonb");
        b.Property(x => x.ResponseSnapshotJson).HasColumnName("response_snapshot_json").HasColumnType("jsonb");
        b.Property(x => x.LatencyMilliseconds).HasColumnName("latency_ms");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.ChargeRequestId, x.CreatedAt });
        b.HasIndex(x => new { x.Outcome, x.CreatedAt });
        b.HasIndex(x => new { x.ProviderName, x.Operation, x.CreatedAt });
        b.HasIndex(x => x.CorrelationId);
    }
}
