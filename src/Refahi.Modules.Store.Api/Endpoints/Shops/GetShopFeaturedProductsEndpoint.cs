using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class GetShopFeaturedProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/shops/{slug}/featured-products", async (
            string slug,
            int? limit,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var actualLimit = limit ?? 12;
            var result = await mediator.Send(new GetShopFeaturedProductsQuery(slug, actualLimit), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetShopFeaturedProducts")
        .WithTags("Store.Shops")
        .Produces<ApiResponse<List<ShopFeaturedProductDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
