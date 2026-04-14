using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.DailyDeals;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class CreateDailyDealEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/daily-deals", async (
            [FromBody] CreateDailyDealCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/daily-deals/{result.Id}",
                ApiResponseHelper.Success(result, "پیشنهاد ویژه با موفقیت ایجاد شد", 201));
        })
        .WithName("Store.CreateDailyDeal")
        .WithTags("Store.DailyDeals")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateDailyDealResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
