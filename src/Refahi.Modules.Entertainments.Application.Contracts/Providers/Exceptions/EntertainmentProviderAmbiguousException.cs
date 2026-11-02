namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;

public sealed class EntertainmentProviderAmbiguousException(
    string message, 
    Exception? innerException = null
): Exception(message, innerException);
