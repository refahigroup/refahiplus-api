using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public class GetWalletBalanceEndpoint: IEndpoint
{

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/{walletId:guid}/balance", async (
            [FromRoute] Guid walletId, 
            IMediator mediator, 
            CancellationToken ct) =>
        {

            var balance = await mediator.Send(new GetWalletBalanceQuery(walletId), ct);

            if (balance is null)
                return Results.NotFound();

            return Results.Ok(balance);


        })
        .RequireAuthorization()
        .WithName("Wallet.GetWalletBalance")
        .WithTags("Wallets")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
