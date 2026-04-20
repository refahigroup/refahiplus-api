using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Cities;

public class GetCitiesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/cities", async (
            int? provinceId,
            IMediator mediator,
            CancellationToken ct,
            bool activeOnly = true) =>
        {
            var query = new GetCitiesQuery(provinceId, activeOnly);
            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.Success(result.Cities));
        })
        .WithName("References.GetCities")
        .WithTags("References.Cities")
        .Produces<ApiResponse<List<CityDto>>>(StatusCodes.Status200OK);
        // Public endpoint — no auth required
    }
}
