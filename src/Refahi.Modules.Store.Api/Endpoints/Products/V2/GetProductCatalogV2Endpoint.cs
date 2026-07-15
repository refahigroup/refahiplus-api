using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products.V2;

public sealed class GetProductCatalogV2Endpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/v2/{moduleSlug}/products", async (
            string moduleSlug,
            string? q,
            int? categoryId,
            Guid? shopId,
            string? shopSlug,
            string? salesModel,
            long? minPriceMinor,
            long? maxPriceMinor,
            string? sort,
            int? pageNumber,
            int? pageSize,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetProductCatalogV2Query(
                moduleId.Value,
                q,
                categoryId,
                shopId,
                shopSlug,
                salesModel,
                minPriceMinor,
                maxPriceMinor,
                string.IsNullOrWhiteSpace(sort) ? "newest" : sort,
                pageNumber ?? 1,
                pageSize ?? 30), ct);

            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.SuccessPaginated(
                    result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Store.V2.GetProductCatalog")
        .WithTags("Store.Products.V2")
        .Produces<PaginatedResponse<ProductCatalogItemV2Dto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}

