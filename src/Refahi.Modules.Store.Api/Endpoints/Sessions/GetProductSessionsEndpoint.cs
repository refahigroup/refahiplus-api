using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Sessions;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Sessions;

public class GetProductSessionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/products/{productId:guid}/sessions", async (
            string moduleSlug,
            Guid productId,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetProductSessionsQuery(productId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetProductSessions")
        .WithTags("Store.Sessions")
        .Produces<ApiResponse<List<ProductSessionDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
