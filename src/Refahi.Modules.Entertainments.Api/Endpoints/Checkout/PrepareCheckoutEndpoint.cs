using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Exceptions;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Checkout;

public sealed class PrepareCheckoutEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapPost(
                "/checkout/prepare",
                async (
                    PrepareCheckoutBody body,
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
            {
                if (!_Helpers.TryUser(c, out var userId))
                    return _Helpers.Unauthorized();

                try
                {
                    var result = await m.Send(new PrepareEntertainmentCheckoutCommand(
                        userId,
                        body.IdempotencyKey,
                        body.ProviderKey
                    ), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));
                }
                catch (EntertainmentPriceChangedException ex)
                {
                    return _Helpers.PriceConflict(ex);
                }
            })
            .WithName("Entertainment.PrepareCheckout")
            .WithTags("Entertainment.Checkout")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record PrepareCheckoutBody(string IdempotencyKey, string? ProviderKey = null);
}
