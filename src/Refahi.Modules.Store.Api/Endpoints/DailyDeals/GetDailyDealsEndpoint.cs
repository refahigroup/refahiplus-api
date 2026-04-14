using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class GetDailyDealsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/daily-deals", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDailyDealsQuery(), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetDailyDeals")
        .WithTags("Store.DailyDeals")
        .Produces<ApiResponse<List<DailyDealDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
