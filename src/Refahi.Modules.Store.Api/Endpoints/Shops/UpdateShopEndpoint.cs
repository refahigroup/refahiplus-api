using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class UpdateShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/provider/shop", async (
            [FromBody] UpdateShopCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "اطلاعات فروشگاه با موفقیت بروزرسانی شد"));
        })
        .WithName("Store.UpdateShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<UpdateShopResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
