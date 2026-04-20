using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Reviews;
using Refahi.Modules.Store.Application.Contracts.Queries.Reviews;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Reviews;

public class GetProductReviewsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/products/{productId:guid}/reviews", async (
            string moduleSlug,
            Guid productId,
            int pageNumber,
            int pageSize,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var query = new GetProductReviewsQuery(
                ProductId: productId,
                PageNumber: pageNumber > 0 ? pageNumber : 1,
                PageSize: pageSize > 0 ? pageSize : 10);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetProductReviews")
        .WithTags("Store.Reviews")
        .Produces<ApiResponse<ProductReviewsResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
