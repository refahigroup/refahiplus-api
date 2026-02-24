using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Services.Payment.MarkPaymentFailed;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api.EndPoints.Payment;

public sealed class MarkPaymentFailedEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("bookings/{bookingId:guid}/payment-failed", async (
            Guid bookingId,
            ISender sender) =>
        {
            await sender.Send(new MarkPaymentFailedCommand(bookingId));

            return Results.Ok();
        })
        .WithName("Hotels.Bookings.PaymentFailed")
        .WithTags("Bookings");
    }
}
