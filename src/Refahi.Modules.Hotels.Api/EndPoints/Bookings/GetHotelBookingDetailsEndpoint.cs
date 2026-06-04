using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.GetBookingDetails;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.Bookings;

public sealed class GetHotelBookingDetailsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("bookings/{bookingId:guid}", async (
            Guid bookingId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetHotelBookingDetailsQuery(bookingId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("Hotels.Bookings.Details")
        .WithTags("Bookings")
        .Produces<HotelBookingDetailsResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}

