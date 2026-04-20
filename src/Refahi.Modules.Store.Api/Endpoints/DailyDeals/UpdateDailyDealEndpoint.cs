using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class UpdateDailyDealEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/daily-deals/{dealId:int}", async (
            int dealId,
            [FromBody] UpdateDailyDealRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateDailyDealCommand(
                dealId,
                body.DiscountPercent,
                body.StartTime,
                body.EndTime,
                body.IsActive);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "پیشنهاد ویژه با موفقیت بروزرسانی شد"));
        })
        .WithName("Store.UpdateDailyDeal")
        .WithTags("Store.DailyDeals")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateDailyDealResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateDailyDealRequest(
    int DiscountPercent,
    string StartTime,
    string EndTime,
    bool IsActive);
