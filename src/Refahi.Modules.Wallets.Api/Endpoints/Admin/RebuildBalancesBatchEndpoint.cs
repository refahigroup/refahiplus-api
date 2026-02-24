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
/// Admin endpoint to rebuild balances for multiple wallets (batch operation).
/// POST /api/wallets/admin/rebuild-balances
/// </summary>
public class RebuildBalancesBatchEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/admin/rebuild-balances", async (
            [FromBody] RebuildBalancesBatchRequest request,
            [FromServices] ISender mediator,
            CancellationToken ct) =>
        {
            var command = new RebuildBalancesBatchCommand(
                Currency: request.Currency,
                OnlyActive: request.OnlyActive);

            try
            {
                var result = await mediator.Send(command, ct);

                return result.Status == CommandStatus.Completed
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(new ErrorResponse("REBUILD_ERROR", "Batch rebuild operation did not complete"));
            }
            catch (ValidationException ex)
            {
                return Results.BadRequest(new ErrorResponse("VALIDATION_ERROR", ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Internal server error",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("RebuildBalancesBatch")
        .WithTags("Admin", "Wallets")
        .Produces<BatchRebuildResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);
    }

    public record RebuildBalancesBatchRequest(
        string? Currency = null,
        bool OnlyActive = true);
}
