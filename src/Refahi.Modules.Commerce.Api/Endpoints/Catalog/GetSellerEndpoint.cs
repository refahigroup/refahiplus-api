using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application.Features.Catalog.GetSeller;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Catalog;

public sealed class GetSellerEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/catalog/sellers/{providerKey}/{sellerKey}", async (
                string providerKey,
                string sellerKey,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var seller = await mediator.Send(new GetCommerceSellerQuery(providerKey, sellerKey), ct);
                return seller is null
                    ? Results.NotFound(ApiResponseHelper.Error("فروشنده یافت نشد", statusCode: 404))
                    : Results.Ok(ApiResponseHelper.Success(seller));
            })
            .WithName("Commerce.GetSeller")
            .WithTags("Commerce.Catalog");
    }
}
