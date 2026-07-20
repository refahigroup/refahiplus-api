namespace Refahi.Modules.Charge.Application.Contracts.Providers;

public enum ChargeProviderFailureKind : short
{
    ProviderRejected = 1,
    Timeout = 2,
    Transport = 3,
    Authentication = 4,
    InvalidResponse = 5,
    Cancelled = 6
}

public sealed class ChargeProviderException : Exception
{
    public ChargeProviderException(
        string message,
        ChargeProviderFailureKind failureKind,
        string operation,
        string stage,
        string correlationId,
        bool retryable,
        bool outcomeAmbiguous,
        Guid? providerCallLogId = null,
        int? httpStatusCode = null,
        int? providerResultCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        Operation = operation;
        Stage = stage;
        CorrelationId = correlationId;
        Retryable = retryable;
        OutcomeAmbiguous = outcomeAmbiguous;
        ProviderCallLogId = providerCallLogId;
        HttpStatusCode = httpStatusCode;
        ProviderResultCode = providerResultCode;
    }

    public ChargeProviderFailureKind FailureKind { get; }
    public string Operation { get; }
    public string Stage { get; }
    public string CorrelationId { get; }
    public bool Retryable { get; }
    public bool OutcomeAmbiguous { get; }
    public Guid? ProviderCallLogId { get; }
    public int? HttpStatusCode { get; }
    public int? ProviderResultCode { get; }
}

public sealed record ProviderCallContext(
    Guid? ChargeRequestId,
    Guid? OrderId,
    Guid? SagaId,
    string CorrelationId);
