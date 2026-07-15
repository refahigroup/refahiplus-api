using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products.V2;
using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products.V2;

public sealed class GetProductDetailV2Endpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/v2/{moduleSlug}/products/{slug}", async (
            string moduleSlug,
            string slug,
            Guid? shopId,
            string? shopSlug,
            string? offerKey,
            Guid? variantId,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetProductDetailV2Query(
                moduleId.Value, slug, shopId, shopSlug, offerKey, variantId), ct);

            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.V2.GetProductDetail")
        .WithTags("Store.Products.V2")
        .Produces<ApiResponse<ProductDetailV2Dto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
