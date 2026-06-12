using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Refahi.Modules.Flights.Api.EndPoints.Bookings;

internal static class FlightEndpointUser
{
    public static bool TryGetUser(HttpContext httpContext, out Guid userId, out string callerRole)
    {
        userId = Guid.Empty;
        callerRole = httpContext.User.IsInRole("Admin") ? "Admin" : "User";

        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return userIdClaim is not null && Guid.TryParse(userIdClaim, out userId);
    }

    public static string? GetIdempotencyKey(HttpContext httpContext)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
    }
}
