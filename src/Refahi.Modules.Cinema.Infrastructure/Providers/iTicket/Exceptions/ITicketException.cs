namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Exceptions;

public class ITicketException : Exception
{
    public ITicketException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
