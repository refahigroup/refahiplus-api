using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class ActivateDailyDealEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/daily-deals/{id:int}/activate", async (
            int id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateDailyDealCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "آفر روز با موفقیت فعال شد"));
        })
        .WithName("Store.ActivateDailyDeal")
        .WithTags("Store.DailyDeals")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ActivateDailyDealResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
