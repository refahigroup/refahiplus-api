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
        Guid? providerCallLogId = null,
        int? httpStatusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
        Operation = operation;
        Stage = stage;
        CorrelationId = correlationId;
        Retryable = retryable;
        ProviderCallLogId = providerCallLogId;
        HttpStatusCode = httpStatusCode;
    }

    public ChargeProviderFailureKind FailureKind { get; }
    public string Operation { get; }
    public string Stage { get; }
    public string CorrelationId { get; }
    public bool Retryable { get; }
    public Guid? ProviderCallLogId { get; }
    public int? HttpStatusCode { get; }
}

public sealed record ProviderCallContext(
    Guid? ChargeRequestId,
    Guid? OrderId,
    Guid? SagaId,
    string CorrelationId);
