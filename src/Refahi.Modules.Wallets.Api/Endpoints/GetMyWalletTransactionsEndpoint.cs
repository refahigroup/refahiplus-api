using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Wallets.Api.Endpoints;

public sealed class GetMyWalletTransactionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/my/transactions", async (
            HttpContext httpContext,
            [FromQuery] int? take,
            [FromQuery] string? walletType,
            [FromQuery] short? operationType,
            [FromQuery] short? entryType,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var result = await mediator.Send(new GetMyWalletTransactionsQuery(
                userId,
                take.GetValueOrDefault(20),
                walletType,
                operationType,
                entryType), ct);

            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("UserOrAdmin")
        .WithName("Wallets.GetMyTransactions")
        .WithTags("Wallets")
        .Produces<ApiResponse<IReadOnlyList<MyWalletTransactionDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
