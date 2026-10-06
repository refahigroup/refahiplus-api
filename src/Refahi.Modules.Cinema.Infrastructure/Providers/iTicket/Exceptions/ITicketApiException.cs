using System.Net;
using System.Text.Json;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Exceptions;

public class ITicketApiException : ITicketException
{
    public ITicketApiException(HttpStatusCode statusCode, string message, int? apiCode = null,
        JsonElement? errors = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ApiCode = apiCode;
        Errors = errors;
    }

    public HttpStatusCode StatusCode { get; }
    public int? ApiCode { get; }
    public JsonElement? Errors { get; }
}
