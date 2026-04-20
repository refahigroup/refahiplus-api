using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Modules;

public class GetModuleBySlugEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/modules/{slug}", async (
            string slug,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetModuleBySlugQuery(slug), ct);
            if (result is null)
                return Results.NotFound();
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetModuleBySlug")
        .WithTags("Store.Modules")
        .Produces<ApiResponse<ModuleDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
