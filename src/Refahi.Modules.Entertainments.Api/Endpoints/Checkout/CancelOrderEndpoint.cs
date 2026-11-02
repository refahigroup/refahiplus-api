using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Api.Endpoints.Catalog;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Order.CancelOrder;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Checkout;

public sealed class CancelOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.
            MapPost(
                "/purchases/{entertainmentsOrderId:guid}/cancel", 
                async (
                    Guid entertainmentsOrderId, 
                    CancelEntertainmentPurchaseBody body, 
                    HttpContext c, 
                    IMediator m, 
                    CancellationToken ct
                ) =>
                {
                    if (!_Helpers.TryUser(c, out var userId))
                        return _Helpers.Unauthorized();

                    var result = await m.Send(new CancelEntertainmentPurchaseCommand(userId, _Helpers.Role(c), entertainmentsOrderId, body.Reason, body.IdempotencyKey), ct);

                    return Results.Ok(ApiResponseHelper.Success(result));

                }
            )
            .WithName("Entertainment.CancelOrder")
            .WithTags("Entertainment.Orders")
            .RequireAuthorization("UserOrAdmin");
    }

    public sealed record CancelEntertainmentPurchaseBody(string? Reason, string IdempotencyKey);
}

