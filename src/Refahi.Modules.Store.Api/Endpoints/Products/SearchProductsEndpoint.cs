using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class SearchProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/products/search", async (
            string moduleSlug,
            string q,
            int pageNumber,
            int pageSize,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var query = new SearchProductsQuery(
                ModuleId: moduleId.Value,
                Query: q ?? string.Empty,
                PageNumber: pageNumber > 0 ? pageNumber : 1,
                PageSize: pageSize > 0 ? pageSize : 20);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data,
                result.PageNumber,
                result.PageSize,
                result.TotalCount));
        })
        .WithName("Store.SearchProducts")
        .WithTags("Store.Products")
        .Produces<PaginatedResponse<ProductSummaryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
