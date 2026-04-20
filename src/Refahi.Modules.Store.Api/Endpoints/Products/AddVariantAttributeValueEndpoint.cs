using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class AddVariantAttributeValueEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/provider/products/{id:guid}/variant-attributes/{attributeId:guid}/values", async (
            Guid id,
            Guid attributeId,
            [FromBody] AddVariantAttributeValueRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddVariantAttributeValueCommand(id, attributeId, body.Value, body.SortOrder);
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/provider/products/{id}/variant-attributes/{attributeId}/values/{result.ValueId}",
                ApiResponseHelper.Success(result, "مقدار ویژگی با موفقیت اضافه شد", 201));
        })
        .WithName("Store.AddVariantAttributeValue")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<AddVariantAttributeValueResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AddVariantAttributeValueRequest(string Value, int SortOrder = 0);
