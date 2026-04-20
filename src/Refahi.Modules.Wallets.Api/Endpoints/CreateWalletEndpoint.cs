using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public class CreateWalletEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/", async (
            [FromBody] CreateWalletRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var command = new CreateWalletCommand(
                OwnerId: userId,
                WalletType: request.WalletType,
                Currency: request.Currency);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "کیف‌پول با موفقیت ایجاد شد"));
        })
        .WithName("Wallets.CreateWallet")
        .WithTags("Wallets")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<CreateWalletResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}

public sealed record CreateWalletRequest(string WalletType, string Currency);
