namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed class CommerceProviderAmbiguousException(
    string message, 
    Exception? innerException = null
): Exception(message, innerException);
