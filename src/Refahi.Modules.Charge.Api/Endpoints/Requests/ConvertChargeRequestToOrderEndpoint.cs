using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class ConvertChargeRequestToOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("charge-requests/{requestId:guid}/order", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId))
                return Results.Unauthorized();

            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(key))
                return Results.BadRequest(ApiResponseHelper.Error("هدر Idempotency-Key الزامی است"));

            var result = await sender.Send(new ConvertChargeRequestToOrderCommand(requestId, userId, key), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سفارش شارژ آماده پرداخت شد"));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Charge.Requests.ConvertToOrder")
        .WithTags("Charge.Requests")
        .Produces<ApiResponse<ConvertChargeRequestToOrderResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
