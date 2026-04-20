using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

public class AdminCreateWalletEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/wallets", async (
            [FromBody] AdminCreateWalletRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CreateWalletCommand(
                OwnerId: request.OwnerId,
                WalletType: request.WalletType,
                Currency: request.Currency);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "کیف‌پول با موفقیت ایجاد شد"));
        })
        .WithName("Wallets.Admin.CreateWallet")
        .WithTags("Wallets.Admin")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateWalletResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record AdminCreateWalletRequest(Guid OwnerId, string WalletType, string Currency);
