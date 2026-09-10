namespace Refahi.Modules.Flights.Application.Contracts.Providers;

public sealed class FlightProviderException : Exception
{
    public FlightProviderException(
        string message,
        string providerName,
        string operation,
        int? httpStatusCode,
        bool retryable,
        Exception? innerException = null
    )
        : base(message, innerException)
    {
        ProviderName = providerName;
        Operation = operation;
        HttpStatusCode = httpStatusCode;
        Retryable = retryable;
    }

    public string ProviderName { get; }

    public string Operation { get; }

    public int? HttpStatusCode { get; }

    public bool Retryable { get; }
}
