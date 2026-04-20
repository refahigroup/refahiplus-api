using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public class GetMyWalletsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/my", async (
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var result = await mediator.Send(new GetMyWalletsQuery(userId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Wallets.GetMyWallets")
        .WithTags("Wallets")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<List<WalletSummaryDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
