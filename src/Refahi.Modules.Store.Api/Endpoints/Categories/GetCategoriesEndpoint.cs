using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Categories;

public class GetCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/categories", async (
            string moduleSlug,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct,
            bool includeInactive = false,
            int? parentId = null) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetCategoriesQuery(includeInactive, moduleId, parentId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetCategories")
        .WithTags("Store.Categories")
        .Produces<ApiResponse<List<CategoryDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // No RequireAuthorization — public endpoint
    }
}
