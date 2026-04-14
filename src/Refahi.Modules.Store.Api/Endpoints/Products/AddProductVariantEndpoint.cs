using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class AddProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/provider/products/{id:guid}/variants", async (
            Guid id,
            [FromBody] AddProductVariantCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var adjustedCommand = command with { ProductId = id };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Created(
                $"/provider/products/{id}/variants/{result.VariantId}",
                ApiResponseHelper.Success(result, "تنوع با موفقیت اضافه شد", 201));
        })
        .WithName("Store.AddProductVariant")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<AddProductVariantResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
