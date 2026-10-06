#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Exceptions;

public sealed class ITicketTransportException : ITicketException
{
    public ITicketTransportException(string message, Exception innerException)
        : base(message, innerException) { }
}
