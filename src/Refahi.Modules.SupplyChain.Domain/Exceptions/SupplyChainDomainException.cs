namespace Refahi.Modules.SupplyChain.Domain.Exceptions;

public class SupplyChainDomainException : Exception
{
    public string ErrorCode { get; }

    public SupplyChainDomainException(string message, string errorCode = "SUPPLYCHAIN_DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
