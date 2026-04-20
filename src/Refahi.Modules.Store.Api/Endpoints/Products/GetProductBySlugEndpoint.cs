using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class GetProductBySlugEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/products/{slug}", async (
            string moduleSlug,
            string slug,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetProductBySlugQuery(slug), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetProductBySlug")
        .WithTags("Store.Products")
        .Produces<ApiResponse<ProductDetailDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
