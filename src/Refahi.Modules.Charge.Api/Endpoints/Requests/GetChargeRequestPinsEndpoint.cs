using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetChargeRequestPinsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("charge-requests/{requestId:guid}/pins", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetChargeRequestPinsQuery(requestId, userId), ct);
            if (result is null)
                return Results.NotFound(ApiResponseHelper.Error("درخواست شارژ یافت نشد", statusCode: 404));

            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.Pragma = "no-cache";
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Charge.Requests.GetPins")
        .WithTags("Charge.Requests")
        .Produces<ApiResponse<IReadOnlyList<ChargePinDeliveryDto>>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
