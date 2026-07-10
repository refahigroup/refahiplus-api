using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.Charge.Domain.Aggregates;
namespace Refahi.Modules.Charge.Infrastructure.Persistence.Configurations;
public sealed class ChargeFulfillmentAttemptConfiguration : IEntityTypeConfiguration<ChargeFulfillmentAttempt>
{
    public void Configure(EntityTypeBuilder<ChargeFulfillmentAttempt> b)
    {
        b.ToTable("charge_fulfillment_attempts"); 
        
        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("id"); 
        
        b.Property(x => x.ChargeRequestId)
            .HasColumnName("charge_request_id");

        b.Property(x => x.Type)
            .HasColumnName("type").HasConversion<short>();
        
        b.Property(x => x.Success)
            .HasColumnName("success");

        b.Property(x => x.EniacResultCode)
            .HasColumnName("eniac_result_code"); 

        b.Property(x => x.OperatorResultCode)
            .HasColumnName("operator_result_code")
            .HasMaxLength(50);

        b.Property(x => x.ProviderRrn)
            .HasColumnName("provider_rrn")
            .HasMaxLength(100); 

        b.Property(x => x.ProviderTraceId)
            .HasColumnName("provider_trace_id")
            .HasMaxLength(150);

        b.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(2000); 

        b.Property(x => x.RequestSnapshotJson)
            .HasColumnName("request_snapshot_json")
            .HasColumnType("jsonb");

        b.Property(x => x.ResponseSnapshotJson)
            .HasColumnName("response_snapshot_json")
            .HasColumnType("jsonb"); 

        b.Property(x => x.LatencyMilliseconds)
            .HasColumnName("latency_ms");

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        b.HasIndex(x => new { x.ChargeRequestId, x.CreatedAt });
    }
}
