using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class CancelChargeRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("charge-requests/{requestId:guid}/cancel", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId))
                return Results.Unauthorized();

            var cancelled = await sender.Send(new CancelChargeRequestCommand(requestId, userId), ct);
            return cancelled
                ? Results.Ok(ApiResponseHelper.Success(new ChargeOperationResponse(requestId), "درخواست لغو شد"))
                : Results.NotFound(ApiResponseHelper.Error("درخواست شارژ یافت نشد", statusCode: 404));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Charge.Requests.Cancel")
        .WithTags("Charge.Requests")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
