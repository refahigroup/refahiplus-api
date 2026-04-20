using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateOrgCreditWallet;
using Refahi.Shared.Presentation;
using System;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

/// <summary>
/// POST /wallets/admin/wallets/org-credit
/// Admin-only: Provision a new OrgCredit wallet for a given owner.
/// </summary>
public class AdminCreateOrgCreditWalletEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/wallets/org-credit", async (
            [FromBody] AdminCreateOrgCreditWalletRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateOrgCreditWalletCommand(
                OwnerId: request.OwnerId,
                Currency: request.Currency,
                AllowedCategoryCode: request.AllowedCategoryCode,
                ContractExpiresAt: request.ContractExpiresAt);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "کیف پول سازمانی با موفقیت ایجاد شد"));
        })
        .WithName("Wallets.Admin.CreateOrgCreditWallet")
        .WithTags("Wallets.Admin")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateOrgCreditWalletResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record AdminCreateOrgCreditWalletRequest(
    Guid OwnerId,
    string Currency,
    string? AllowedCategoryCode,
    DateTimeOffset? ContractExpiresAt);
