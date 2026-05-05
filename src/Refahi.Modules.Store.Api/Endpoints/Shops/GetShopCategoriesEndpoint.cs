using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class GetShopCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/shops/{slug}/categories", async (
            string slug,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetShopCategoriesQuery(slug), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetShopCategories")
        .WithTags("Store.Shops")
        .Produces<ApiResponse<List<ShopCategoryDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
