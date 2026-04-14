using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class CreateShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/shops", async (
            [FromBody] CreateShopCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/store/shops/{result.Id}",
                ApiResponseHelper.Success(result, "فروشگاه با موفقیت ایجاد شد", 201));
        })
        .WithName("Store.CreateShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<CreateShopResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
