using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Services.ProvisionalBooking;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.ProvisionalBooking;

public sealed class CreateProvisionalBookingEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("bookings/provisional", async (
            [FromBody] CreateProvisionalBookingRequest body,
            ISender sender) =>
        {
            var command = new CreateProvisionalBookingCommand(
                body.HotelId,
                body.RoomId,
                body.CheckIn,
                body.CheckOut,
                body.RoomsCount,
                body.Guests,
                body.BoardType
            );

            var result = await sender.Send(command);

            return Results.Ok(result);

        })
        .Produces<ProvisionalBookingResponse>()
        .WithName("Hotels.Bookings.Provisional")
        .WithTags("Bookings");
    }
}

public sealed class CreateProvisionalBookingRequest
{
    public long HotelId { get; set; }
    public long RoomId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int RoomsCount { get; set; }
    public IEnumerable<GuestDto> Guests { get; set; } = Enumerable.Empty<GuestDto>();
    public string BoardType { get; set; } = default!;
}