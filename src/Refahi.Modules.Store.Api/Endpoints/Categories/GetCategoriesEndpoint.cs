using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Categories;
using Refahi.Modules.Store.Application.Contracts.Queries.Categories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Categories;

public class GetCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/categories", async (
            bool includeInactive,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategoriesQuery(includeInactive), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetCategories")
        .WithTags("Store.Categories")
        .Produces<ApiResponse<List<CategoryDto>>>(StatusCodes.Status200OK);
        // No RequireAuthorization — public endpoint
    }
}
