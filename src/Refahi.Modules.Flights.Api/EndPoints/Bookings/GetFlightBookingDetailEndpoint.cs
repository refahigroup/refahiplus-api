using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Application.Features.Bookings.GetBookingDetail;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Flights.Api.EndPoints.Bookings;

public sealed class GetFlightBookingDetailEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/bookings/{bookingId:guid}", async (
            Guid bookingId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!FlightEndpointUser.TryGetUser(httpContext, out var userId, out var callerRole))
                return Results.Unauthorized();

            var result = await sender.Send(new GetFlightBookingDetailQuery(
                bookingId,
                userId,
                callerRole), cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponseHelper.Error("رزرو پرواز یافت نشد.", statusCode: StatusCodes.Status404NotFound))
                : Results.Ok(ApiResponseHelper.Success(result, "جزئیات رزرو پرواز دریافت شد."));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Flights.Bookings.GetDetail")
        .WithTags("Flights.Bookings")
        .Produces<ApiResponse<FlightBookingDetailDto>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
