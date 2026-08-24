using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Cart;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Checkout;

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
                    var result = await m.Send(new PrepareCommerceCheckoutCommand(
                        userId,
                        body.RecipientName,
                        body.RecipientMobile,
                        body.IdempotencyKey
                    ), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));
                }
                catch (CommercePriceChangedException ex)
                {
                    return _Helpers.PriceConflict(ex);
                }
            })
            .WithName("Commerce.PrepareCheckout")
            .WithTags("Commerce.Checkout")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record PrepareCheckoutBody(string RecipientName, string RecipientMobile, string IdempotencyKey);
}
