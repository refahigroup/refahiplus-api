namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;

public sealed class CommerceProviderAmbiguousException(
    string message, 
    Exception? innerException = null
): Exception(message, innerException);
