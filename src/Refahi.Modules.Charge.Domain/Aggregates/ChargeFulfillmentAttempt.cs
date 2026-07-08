using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Domain.Aggregates;

public sealed class ChargeFulfillmentAttempt
{
    private ChargeFulfillmentAttempt() { }
    public Guid Id { get; private set; }
    public Guid ChargeRequestId { get; private set; }
    public FulfillmentAttemptType Type { get; private set; }
    public bool Success { get; private set; }
    public int? EniacResultCode { get; private set; }
    public string? OperatorResultCode { get; private set; }
    public string? ProviderRrn { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public string? Message { get; private set; }
    public string RequestSnapshotJson { get; private set; } = "{}";
    public string ResponseSnapshotJson { get; private set; } = "{}";
    public long LatencyMilliseconds { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ChargeFulfillmentAttempt Create(Guid requestId, FulfillmentAttemptType type, bool success, int? eniacCode,
        string? operatorCode, string? rrn, string? traceId, string? message, string requestJson, string responseJson,
        long latencyMilliseconds, DateTime nowUtc) => new()
        {
            Id = Guid.NewGuid(), ChargeRequestId = requestId, Type = type, Success = success,
            EniacResultCode = eniacCode, OperatorResultCode = operatorCode, ProviderRrn = rrn,
            ProviderTraceId = traceId, Message = message, RequestSnapshotJson = requestJson,
            ResponseSnapshotJson = responseJson, LatencyMilliseconds = latencyMilliseconds, CreatedAt = nowUtc
        };
}
