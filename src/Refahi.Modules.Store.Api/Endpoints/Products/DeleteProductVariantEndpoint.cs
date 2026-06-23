using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class DeleteProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/provider/products/{id:guid}/variants/{variantId:guid}", async (
            Guid id,
            Guid variantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DeleteProductVariantCommand(id, variantId), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "تنوع با موفقیت حذف شد"));
        })
        .WithName("Store.DeleteProductVariant")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}