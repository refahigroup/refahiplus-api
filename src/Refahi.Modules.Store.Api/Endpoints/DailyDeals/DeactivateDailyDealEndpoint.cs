using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class DeactivateDailyDealEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/daily-deals/{dealId:int}/deactivate", async (
            int dealId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DeactivateDailyDealCommand(dealId);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "پیشنهاد ویژه با موفقیت غیرفعال شد"));
        })
        .WithName("Store.DeactivateDailyDeal")
        .WithTags("Store.DailyDeals")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<DeactivateDailyDealResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
