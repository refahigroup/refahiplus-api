using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUpOrgCredit;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

/// <summary>
/// POST /wallets/admin/wallets/{walletId}/org-credit-topup
/// Admin-only: Top up an OrgCredit wallet.
/// </summary>
public class AdminTopUpOrgCreditEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/wallets/{walletId:guid}/org-credit-topup", async (
            Guid walletId,
            [FromBody] AdminTopUpOrgCreditRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new TopUpOrgCreditCommand(
                WalletId: walletId,
                AmountMinor: request.AmountMinor,
                Currency: request.Currency,
                IdempotencyKey: request.IdempotencyKey,
                MetadataJson: request.MetadataJson,
                ExternalReference: request.ExternalReference);

            var result = await mediator.Send(command, ct);

            if (result.Data is null)
                return Results.StatusCode(StatusCodes.Status202Accepted);

            return Results.Ok(ApiResponseHelper.Success(result.Data, "شارژ کیف پول سازمانی با موفقیت انجام شد"));
        })
        .WithName("Wallets.Admin.TopUpOrgCredit")
        .WithTags("Wallets.Admin")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AdminTopUpOrgCreditRequest(
    long AmountMinor,
    string Currency,
    string IdempotencyKey,
    string? MetadataJson = null,
    string? ExternalReference = null);
