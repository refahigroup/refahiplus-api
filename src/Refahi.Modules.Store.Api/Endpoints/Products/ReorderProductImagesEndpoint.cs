using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class ReorderProductImagesEndpoint : IEndpoint
{
    public sealed record ReorderProductImagesRequest(List<ProductImageOrderItem> Items);

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/provider/products/{id:guid}/images/reorder", async (
            Guid id,
            [FromBody] ReorderProductImagesRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new ReorderProductImagesCommand(id, request.Items), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "ترتیب تصاویر با موفقیت به‌روزرسانی شد"));
        })
        .WithName("Store.ReorderProductImages")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
