using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class AdminListDailyDealsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/daily-deals", async (
            IMediator mediator,
            int? moduleId,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new AdminGetDailyDealsQuery(moduleId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Store.Admin.ListDailyDeals")
        .WithTags("Store.Admin");
    }
}
