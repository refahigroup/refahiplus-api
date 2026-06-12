using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Application.Features.Bookings.PrepareOrder;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Bookings;

public sealed class PrepareFlightOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/bookings/{bookingId:guid}/prepare-order", async (
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

            var result = await sender.Send(new PrepareFlightOrderCommand(
                bookingId,
                userId,
                callerRole,
                idempotencyKey), cancellationToken);

            return Results.Ok(ApiResponseHelper.Success(result, "سفارش رزرو پرواز آماده پرداخت شد."));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Flights.Bookings.PrepareOrder")
        .WithTags("Flights.Bookings")
        .Produces<ApiResponse<PrepareFlightOrderResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
