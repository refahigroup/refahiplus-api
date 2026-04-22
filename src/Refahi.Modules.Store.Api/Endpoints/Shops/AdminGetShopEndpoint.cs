using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class AdminGetShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/shops/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new AdminGetShopQuery(id), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.GetShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ShopDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
