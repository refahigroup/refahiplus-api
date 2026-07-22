using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Categories;

public sealed class GetStoreCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/categories", async (
            string moduleSlug,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (!moduleId.HasValue)
                return Results.NotFound();

            var categories = await mediator.Send(new GetStoreCategoriesQuery(moduleId.Value), ct);
            return Results.Ok(ApiResponseHelper.Success(categories));
        })
        .WithName("Store.GetStoreCategories")
        .WithTags("Store.Categories")
        .Produces<ApiResponse<IReadOnlyList<StoreCategoryDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
