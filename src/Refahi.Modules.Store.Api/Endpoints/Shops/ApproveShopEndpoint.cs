using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class ApproveShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/shops/{id:guid}/approve", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ApproveShopCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "فروشگاه با موفقیت تایید شد"));
        })
        .WithName("Store.ApproveShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ApproveShopResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
