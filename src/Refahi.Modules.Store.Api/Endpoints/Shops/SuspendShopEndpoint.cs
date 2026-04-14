using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class SuspendShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/shops/{id:guid}/suspend", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new SuspendShopCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "فروشگاه با موفقیت تعلیق شد"));
        })
        .WithName("Store.SuspendShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<SuspendShopResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
