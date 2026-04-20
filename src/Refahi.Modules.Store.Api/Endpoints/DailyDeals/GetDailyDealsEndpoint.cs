using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class GetDailyDealsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/daily-deals", async (
            string moduleSlug,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetDailyDealsQuery(moduleId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetDailyDeals")
        .WithTags("Store.DailyDeals")
        .Produces<ApiResponse<List<DailyDealDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
