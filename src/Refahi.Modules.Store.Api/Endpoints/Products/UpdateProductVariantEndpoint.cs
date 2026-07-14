using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public sealed class UpdateProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/provider/products/{id:guid}/variants/{variantId:guid}", async (
            Guid id,
            Guid variantId,
            [FromBody] UpdateProductVariantRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new UpdateProductVariantCommand(
                id,
                variantId,
                body.Combinations,
                body.ImageUrl,
                body.StockCount,
                body.PriceMinor,
                body.DiscountedPriceMinor,
                body.Sku,
                body.FromDate,
                body.ToDate,
                body.CapacityType,
                body.Capacity), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null, "تنوع با موفقیت ویرایش شد"));
        })
        .WithName("Store.UpdateProductVariant")
        .WithTags("Store.Products")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<object>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateProductVariantRequest(
    List<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    long PriceMinor,
    long? DiscountedPriceMinor,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    VariantCapacityType CapacityType = VariantCapacityType.Unlimited,
    int? Capacity = null);
