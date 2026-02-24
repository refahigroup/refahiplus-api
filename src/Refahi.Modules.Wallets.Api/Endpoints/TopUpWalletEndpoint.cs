using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public class TopUpWalletEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/{walletId:guid}/topups", async (
            Guid walletId,
            TopUpWalletBody request,
            HttpRequest httpRequest,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var idemKeyValues))
                return Results.BadRequest(new ProblemDetails { Title = "Missing Idempotency-Key header" });

            var idemKey = idemKeyValues.ToString();

            var command = new TopUpWalletCommand(
                WalletId: walletId,
                AmountMinor: request.AmountMinor,
                Currency: request.Currency,
                IdempotencyKey: idemKey,
                MetadataJson: request.MetadataJson,
                ExternalReference: request.ExternalReference);

            try
            {
                var resp = await mediator.Send(command, ct);

                if (resp.Status == CommandStatus.InProgress)
                    return Results.Conflict(new ProblemDetails { Title = "IN_PROGRESS", Detail = "The operation is currently pending. Retry later using the same Idempotency-Key." });

                return Results.Ok(resp.Data);
            }
            catch (WalletNotFoundException)
            {
                return Results.NotFound(new ProblemDetails { Title = "Wallet not found" });
            }
            catch (WalletCurrencyMismatchException cm)
            {
                return Results.BadRequest(new ProblemDetails { Title = cm.Code, Detail = cm.Message });
            }
            catch (IdempotencyKeyConflictException ic)
            {
                return Results.Conflict(new ProblemDetails { Title = ic.Code, Detail = ic.Message });
            }
            catch (WalletOperationNotAllowedException wo)
            {
                return Results.Conflict(new ProblemDetails { Title = wo.Code, Detail = wo.Message });
            }
        })
        .RequireAuthorization()
        .WithName("TopUpWallet")
        .WithTags("Wallets")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    public sealed record TopUpWalletBody(
        long AmountMinor,
        string Currency,
        string? MetadataJson,
        string? ExternalReference);
}
