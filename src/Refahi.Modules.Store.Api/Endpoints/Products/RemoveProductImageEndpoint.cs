using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class RemoveProductImageEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/provider/products/{id:guid}/images/{imageId:int}", async (
            Guid id,
            int imageId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RemoveProductImageCommand(id, imageId), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "تصویر با موفقیت حذف شد"));
        })
        .WithName("Store.RemoveProductImage")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
