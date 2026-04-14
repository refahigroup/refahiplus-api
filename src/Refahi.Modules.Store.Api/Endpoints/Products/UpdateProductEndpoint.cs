using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class UpdateProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/provider/products/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var adjustedCommand = command with { Id = id };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "محصول با موفقیت به‌روزرسانی شد"));
        })
        .WithName("Store.UpdateProduct")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<UpdateProductResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
