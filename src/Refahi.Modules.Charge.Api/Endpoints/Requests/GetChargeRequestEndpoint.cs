using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetChargeRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("charge-requests/{requestId:guid}", async (Guid requestId, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!ChargeEndpointHelpers.TryUserId(http, out var userId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetChargeRequestQuery(requestId, userId), ct);
            return result is null
                ? Results.NotFound(ApiResponseHelper.Error("درخواست شارژ یافت نشد", statusCode: 404))
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Charge.Requests.Get")
        .WithTags("Charge.Requests")
        .Produces<ApiResponse<ChargeRequestDetailDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }
}
