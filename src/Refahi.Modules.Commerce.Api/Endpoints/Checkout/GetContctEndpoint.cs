using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Cart;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Checkout.GetContact;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Checkout;

public sealed class GetContctEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/checkout/contact",
                async (
                    HttpContext c,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    var result = await m.Send(new GetCommerceCheckoutContactQuery(userId), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));

                }
            )
            .WithName("Commerce.GetCheckoutContact")
            .WithTags("Commerce.Checkout")
            .RequireAuthorization("UserOrAdmin");
    }
}

