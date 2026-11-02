using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Provinces;

public class GetProvincesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet(
                "/provinces",
                async (IMediator mediator, CancellationToken ct, bool activeOnly = true, string? q = null) =>
                {
                    var query = new GetProvincesQuery(activeOnly, q);
                    try
                    {
                        var result = await mediator.Send(query, ct);
                        return Results.Ok(ApiResponseHelper.Success(result.Provinces));
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return Results.StatusCode(499);
                    }
                }
            )
            .WithName("References.GetProvinces")
            .WithTags("References.Provinces")
            .Produces<ApiResponse<List<ProvinceDto>>>(StatusCodes.Status200OK);
        // Public endpoint — no auth required
    }
}
