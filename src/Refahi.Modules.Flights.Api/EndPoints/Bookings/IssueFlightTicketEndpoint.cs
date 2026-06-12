using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Application.Features.Bookings.IssueTicket;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Bookings;

public sealed class IssueFlightTicketEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/bookings/{bookingId:guid}/issue", async (
            Guid bookingId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!FlightEndpointUser.TryGetUser(httpContext, out var userId, out var callerRole))
                return Results.Unauthorized();

            var idempotencyKey = FlightEndpointUser.GetIdempotencyKey(httpContext);
            if (idempotencyKey is null)
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است."));

            var result = await sender.Send(new IssueFlightTicketCommand(
                bookingId,
                userId,
                callerRole,
                idempotencyKey), cancellationToken);

            return Results.Ok(ApiResponseHelper.Success(result, "بلیط پرواز با موفقیت صادر شد."));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Flights.Bookings.Issue")
        .WithTags("Flights.Bookings")
        .Produces<ApiResponse<IssueFlightTicketResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
