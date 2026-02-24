using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

/// <summary>
/// Admin endpoint to rebuild balance for a single wallet from ledger.
/// POST /api/wallets/admin/{walletId}/rebuild-balance
/// </summary>
public class RebuildBalanceEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/admin/{walletId:guid}/rebuild-balance", async (
            [FromRoute] Guid walletId,
            [FromServices] ISender mediator,
            CancellationToken ct) =>
        {
            var command = new RebuildBalanceCommand(walletId);

            try
            {
                var result = await mediator.Send(command, ct);

                return result.Status == CommandStatus.Completed
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(new ErrorResponse("REBUILD_ERROR", "Rebuild operation did not complete"));
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new ErrorResponse("VALIDATION_ERROR", ex.Message));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return Results.NotFound(new ErrorResponse("WALLET_NOT_FOUND", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Internal server error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("RebuildBalance")
        .WithTags("Admin", "Wallets")
        .Produces<RebuildBalanceResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}
