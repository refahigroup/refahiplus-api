using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.ConvertHotelRequestToOrder;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Hotels.Api.EndPoints.HotelRequests;

public sealed class ConvertHotelRequestToOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("hotel-requests/{requestId:guid}/order", async (
            Guid requestId,
            HttpContext httpContext,
            ILogger<ConvertHotelRequestToOrderEndpoint> logger,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["RequestId"] = httpContext.TraceIdentifier,
                ["UserId"] = userId,
                ["SagaId"] = null,
                ["HotelRequestId"] = requestId,
                ["OrderId"] = null,
                ["ProviderBookingCode"] = null
            });

            var result = await sender.Send(new ConvertHotelRequestToOrderCommand(
                requestId,
                userId,
                idempotencyKey), ct);

            return Results.Ok(ApiResponseHelper.Success(result, "سفارش هتل آماده پرداخت شد"));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Hotels.HotelRequests.ConvertToOrder")
        .WithTags("Hotels.HotelRequests")
        .Produces<ApiResponse<ConvertHotelRequestToOrderResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static bool TryGetUserId(HttpContext httpContext, out Guid userId)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out userId);
    }
}
