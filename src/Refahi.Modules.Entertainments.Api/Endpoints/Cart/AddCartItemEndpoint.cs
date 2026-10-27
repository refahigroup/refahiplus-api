using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Exceptions;
using Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Cart;

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
                    var result = await m.Send(new AddEntertainmentCartItemCommand(
                        userId,
                        body.ProviderKey,
                        body.SupplierKey,
                        body.ProgramKey,
                        body.OfferKey,
                        body.PurchaseOptionKey,
                        body.Quantity,
                        body.ExpectedUnitPriceMinor
                    ), ct);

                    return Results.Ok(ApiResponseHelper.Success(result, "آیتم به سبد اضافه شد"));
                }
                catch (EntertainmentPriceChangedException ex)
                {
                    return _Helpers.PriceConflict(ex);
                }
            })
            .WithName("Entertainment.AddCartItem")
            .WithTags("Entertainment.Cart")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record AddCartItemBody(string ProviderKey, string SupplierKey, string ProgramKey, string OfferKey, string PurchaseOptionKey, int Quantity, long ExpectedUnitPriceMinor);
}

