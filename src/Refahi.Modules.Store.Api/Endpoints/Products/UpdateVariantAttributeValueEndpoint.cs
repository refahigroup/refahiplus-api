using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public sealed class UpdateVariantAttributeValueEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/provider/products/{id:guid}/variant-attributes/{attributeId:guid}/values/{valueId:guid}", async (
            Guid id,
            Guid attributeId,
            Guid valueId,
            [FromBody] UpdateVariantAttributeValueRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(
                new UpdateVariantAttributeValueCommand(id, attributeId, valueId, body.Value, body.SortOrder),
                ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "مقدار ویژگی با موفقیت ویرایش شد"));
        })
        .WithName("Store.UpdateVariantAttributeValue")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateVariantAttributeValueRequest(string Value, int SortOrder = 0);
