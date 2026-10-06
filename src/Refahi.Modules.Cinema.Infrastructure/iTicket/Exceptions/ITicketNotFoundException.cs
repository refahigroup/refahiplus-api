#nullable enable

using System.Net;
using System.Text.Json;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket.Exceptions;

public sealed class ITicketNotFoundException : ITicketApiException
{
    public ITicketNotFoundException(HttpStatusCode statusCode, string message, int? apiCode = null,
        JsonElement? errors = null)
        : base(statusCode, message, apiCode, errors) { }
}
