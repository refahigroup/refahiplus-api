using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Domain.Aggregates;

public sealed class ProviderCallLog
{
    private ProviderCallLog() { }

    public Guid Id { get; private set; }
    public Guid? ChargeRequestId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? SagaId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string Stage { get; private set; } = string.Empty;
    public ProviderCallOutcome Outcome { get; private set; }
    public string HttpMethod { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public int? HttpStatusCode { get; private set; }
    public int? ProviderResultCode { get; private set; }
    public string? OperatorResultCode { get; private set; }
    public bool Retryable { get; private set; }
    public int AttemptNumber { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? ExceptionType { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string RequestSnapshotJson { get; private set; } = "{}";
    public string ResponseSnapshotJson { get; private set; } = "{}";
    public long LatencyMilliseconds { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ProviderCallLog Create(
        Guid? chargeRequestId, Guid? orderId, Guid? sagaId,
        string providerName, string operation, string stage, ProviderCallOutcome outcome,
        string httpMethod, string endpoint, int? httpStatusCode,
        int? providerResultCode, string? operatorResultCode, bool retryable,
        int attemptNumber, string correlationId, string? exceptionType, string? errorMessage,
        string requestSnapshotJson, string responseSnapshotJson, long latencyMilliseconds,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(operation) ||
            string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(correlationId))
            throw new InvalidOperationException("اطلاعات لاگ تامین‌کننده کامل نیست");

        return new ProviderCallLog
        {
            Id = Guid.NewGuid(),
            ChargeRequestId = chargeRequestId,
            OrderId = orderId,
            SagaId = sagaId,
            ProviderName = providerName.Trim(),
            Operation = operation.Trim(),
            Stage = stage.Trim(),
            Outcome = outcome,
            HttpMethod = httpMethod.Trim(),
            Endpoint = endpoint.Trim(),
            HttpStatusCode = httpStatusCode,
            ProviderResultCode = providerResultCode,
            OperatorResultCode = operatorResultCode,
            Retryable = retryable,
            AttemptNumber = Math.Max(1, attemptNumber),
            CorrelationId = correlationId.Trim(),
            ExceptionType = exceptionType,
            ErrorMessage = errorMessage,
            RequestSnapshotJson = string.IsNullOrWhiteSpace(requestSnapshotJson) ? "{}" : requestSnapshotJson,
            ResponseSnapshotJson = string.IsNullOrWhiteSpace(responseSnapshotJson) ? "{}" : responseSnapshotJson,
            LatencyMilliseconds = Math.Max(0, latencyMilliseconds),
            CreatedAt = nowUtc
        };
    }
}
