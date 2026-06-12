using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Application.Features.Bookings.CreateBooking;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Bookings;

public sealed class CreateFlightBookingEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/bookings", async (
            CreateFlightBookingRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!FlightEndpointUser.TryGetUser(httpContext, out var userId, out _))
                return Results.Unauthorized();

            var idempotencyKey = FlightEndpointUser.GetIdempotencyKey(httpContext);
            if (idempotencyKey is null)
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است."));

            var result = await sender.Send(new CreateFlightBookingCommand(
                userId,
                request.OfferToken,
                request.Contact,
                request.Passengers,
                idempotencyKey), cancellationToken);

            return Results.Ok(ApiResponseHelper.Success(result, "رزرو پرواز با موفقیت ایجاد شد."));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Flights.Bookings.Create")
        .WithTags("Flights.Bookings")
        .Produces<ApiResponse<FlightBookingDetailDto>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record CreateFlightBookingRequest(
    string OfferToken,
    FlightBookingContactInput Contact,
    IReadOnlyCollection<FlightBookingPassengerInput> Passengers);
