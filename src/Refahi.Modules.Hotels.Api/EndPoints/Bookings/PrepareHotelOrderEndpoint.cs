using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.PrepareOrder;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Hotels.Api.EndPoints.Bookings;

public sealed class PrepareHotelOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("bookings/{bookingId:guid}/order", async (
            Guid bookingId,
            HttpContext httpContext,
            ISender sender,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            var result = await sender.Send(new PrepareHotelOrderCommand(
                bookingId,
                userId,
                idempotencyKey), ct);

            return Results.Ok(ApiResponseHelper.Success(result, "سفارش رزرو هتل آماده پرداخت شد"));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Hotels.Bookings.PrepareOrder")
        .WithTags("Bookings")
        .Produces<ApiResponse<PrepareHotelOrderResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
