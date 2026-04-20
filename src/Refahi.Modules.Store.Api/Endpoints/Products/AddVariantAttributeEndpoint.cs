using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class AddVariantAttributeEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/provider/products/{id:guid}/variant-attributes", async (
            Guid id,
            [FromBody] AddVariantAttributeRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddVariantAttributeCommand(id, body.Name, body.SortOrder);
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/provider/products/{id}/variant-attributes/{result.AttributeId}",
                ApiResponseHelper.Success(result, "ویژگی تنوع با موفقیت اضافه شد", 201));
        })
        .WithName("Store.AddVariantAttribute")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<AddVariantAttributeResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AddVariantAttributeRequest(string Name, int SortOrder = 0);
