using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Categories;

public class GetCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/categories", async (
            IMediator mediator,
            CancellationToken ct,
            string? categoryCodePrefix = null,
            int? parentId = null,
            bool includeInactive = false) =>
        {
            var query = new GetCategoriesQuery(categoryCodePrefix, parentId, includeInactive);
            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("References.GetCategories")
        .WithTags("References.Categories")
        .Produces<ApiResponse<List<CategoryDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
