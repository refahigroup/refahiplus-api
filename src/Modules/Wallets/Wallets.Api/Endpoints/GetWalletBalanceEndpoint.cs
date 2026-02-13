using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wallets.Application.Contracts.Features.GetBalance;

namespace Wallets.Api.Endpoints;

public class GetWalletBalanceEndpoint: IEndpoint
{

    public void Map(IEndpointRouteBuilder app)
    {
        if (app == null)
            return;

        app.MapGet("/{walletId:guid}/balance", async (
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
        .WithTags("Wallet")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
