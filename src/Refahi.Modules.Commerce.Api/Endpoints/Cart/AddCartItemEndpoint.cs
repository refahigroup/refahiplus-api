using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Application.Features.Cart.AddItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

public sealed class AddCartItemEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost(
            "/cart/items",
            async (
                AddCartItemBody body,
                HttpContext c,
                IMediator m,
                CancellationToken ct
            ) =>
            {
                if (!_Helpers.TryUser(c, out var userId))
                    return _Helpers.Unauthorized();

                try
                {
                    var result = await m.Send(new AddCommerceCartItemCommand(
                        userId,
                        body.ProviderKey,
                        body.SellerKey,
                        body.ProductKey,
                        body.OfferKey,
                        body.PurchaseOptionKey,
                        body.Quantity,
                        body.ExpectedUnitPriceMinor
                    ), ct);

                    return Results.Ok(ApiResponseHelper.Success(result, "آیتم به سبد اضافه شد"));
                }
                catch (CommercePriceChangedException ex)
                {
                    return _Helpers.PriceConflict(ex);
                }
            })
            .WithName("Commerce.AddCartItem")
            .WithTags("Commerce.Cart")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record AddCartItemBody(string ProviderKey, string SellerKey, string ProductKey, string OfferKey, string PurchaseOptionKey, int Quantity, long ExpectedUnitPriceMinor);
}

