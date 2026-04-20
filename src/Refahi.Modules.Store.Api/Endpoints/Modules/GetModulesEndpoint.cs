using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Modules;
using Refahi.Modules.Store.Application.Contracts.Queries.Modules;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Modules;

public class GetModulesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/modules", async (
            IMediator mediator,
            CancellationToken ct,
            bool includeInactive = false) =>
        {
            var result = await mediator.Send(new GetModulesQuery(includeInactive), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetModules")
        .WithTags("Store.Modules")
        .Produces<ApiResponse<List<ModuleDto>>>(StatusCodes.Status200OK);
    }
}
