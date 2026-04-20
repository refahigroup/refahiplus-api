using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyWallets;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

public class AdminGetUserWalletsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/users/{userId:guid}/wallets", async (
            Guid userId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetMyWalletsQuery(userId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Wallets.Admin.GetUserWallets")
        .WithTags("Wallets.Admin")
        .Produces<ApiResponse<List<WalletSummaryDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
