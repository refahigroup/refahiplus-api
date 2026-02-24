using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Api.Models;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

/// <summary>
/// Admin endpoint to detect drift without modifying data (read-only).
/// GET /api/wallets/admin/{walletId}/drift
/// </summary>
public class DetectDriftEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/admin/{walletId:guid}/drift", async (
            [FromRoute] Guid walletId,
            [FromServices] ISender mediator,
            CancellationToken ct) =>
        {
            var query = new DetectDriftQuery(walletId);

            try
            {
                var result = await mediator.Send(query, ct);

                return result.Status == CommandStatus.Completed
                    ? Results.Ok(result.Data)
                    : Results.BadRequest(new ErrorResponse("QUERY_ERROR", "Query failed"));
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
        .WithName("DetectDrift")
        .WithTags("Wallets", "Admin")
        .Produces<DriftReportResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}
