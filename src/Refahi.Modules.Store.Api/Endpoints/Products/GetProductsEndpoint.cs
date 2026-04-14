using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class GetProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/products", async (
            int? categoryId,
            Guid? shopId,
            long? minPrice,
            long? maxPrice,
            short? salesModel,
            int pageNumber,
            int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetProductsQuery(
                CategoryId: categoryId,
                ShopId: shopId,
                MinPriceMinor: minPrice,
                MaxPriceMinor: maxPrice,
                SalesModel: salesModel,
                PageNumber: pageNumber > 0 ? pageNumber : 1,
                PageSize: pageSize > 0 ? pageSize : 20);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data,
                result.PageNumber,
                result.PageSize,
                result.TotalCount));
        })
        .WithName("Store.GetProducts")
        .WithTags("Store.Products")
        .Produces<PaginatedResponse<ProductSummaryDto>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
