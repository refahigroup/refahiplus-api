using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public class GetWalletTransactionsEndpoint: IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/{walletId:guid}/transactions", async (
            [FromRoute] Guid walletId, 
            [FromQuery] int? take,
            IMediator mediator,
            CancellationToken ct) =>
        {

            if (!take.HasValue || take == 0)
                take = 20;

            var transactions = await mediator.Send(new GetWalletTransactionsQuery(walletId, take.Value), ct);

            if(transactions == null)
                return Results.NotFound();

            return Results.Ok(new 
            { 
                walletId, 
                take, 
                transactions 
            });
        })
        .RequireAuthorization()
        .WithName("Wallet.GetWalletTransactions")
        .WithTags("Wallets")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
