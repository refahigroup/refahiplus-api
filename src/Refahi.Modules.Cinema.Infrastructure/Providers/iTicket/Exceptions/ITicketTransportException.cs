namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Exceptions;

public sealed class ITicketTransportException : ITicketException
{
    public ITicketTransportException(string message, Exception innerException)
        : base(message, innerException) { }
}
