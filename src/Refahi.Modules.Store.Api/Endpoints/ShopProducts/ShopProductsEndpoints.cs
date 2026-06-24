using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;
using Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.ShopProducts;

public class GetShopProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/shops/{shopId:guid}/products", async (
            Guid shopId,
            bool? isActive,
            int pageNumber,
            int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetShopProductsQuery(
                shopId,
                isActive,
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Store.Admin.GetShopProducts")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ShopProductsPagedResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public class AddProductToShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/shops/{shopId:guid}/products", async (
            Guid shopId,
            AddProductToShopRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddProductToShopCommand(shopId, request.ProductId, request.Price, request.DiscountedPrice, request.Description);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.AddProductToShop")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<AddProductToShopResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public class RemoveProductFromShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/shops/{shopId:guid}/products/{productId:guid}", async (
            Guid shopId,
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new RemoveProductFromShopCommand(shopId, productId);
            await mediator.Send(command, ct);
            return Results.NoContent();
        })
        .WithName("Store.Admin.RemoveProductFromShop")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class EnableShopProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/shops/{shopId:guid}/products/{productId:guid}/enable", async (
            Guid shopId,
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new EnableShopProductCommand(shopId, productId);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null));
        })
        .WithName("Store.Admin.EnableShopProduct")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public class DisableShopProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/shops/{shopId:guid}/products/{productId:guid}/disable", async (
            Guid shopId,
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new DisableShopProductCommand(shopId, productId);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null));
        })
        .WithName("Store.Admin.DisableShopProduct")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public class UpdateShopProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/shops/{shopId:guid}/products/{productId:guid}", async (
            Guid shopId,
            Guid productId,
            UpdateShopProductRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateShopProductCommand(shopId, productId, request.Price, request.DiscountedPrice, request.Description);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null));
        })
        .WithName("Store.Admin.UpdateShopProduct")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class ListShopProductVariantsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/shops/{shopId:guid}/products/{productId:guid}/variants", async (
            Guid shopId,
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ListShopProductVariantsQuery(shopId, productId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.ListShopProductVariants")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<IReadOnlyList<ShopProductVariantDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class UpsertShopProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/shops/{shopId:guid}/products/{productId:guid}/variants/{variantId:guid}", async (
            Guid shopId,
            Guid productId,
            Guid variantId,
            UpsertShopProductVariantRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpsertShopProductVariantCommand(
                shopId,
                productId,
                variantId,
                request.PriceMinor,
                request.DiscountedPriceMinor,
                request.IsActive);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.UpsertShopProductVariant")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ShopProductVariantDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class EnableShopProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/shops/{shopId:guid}/products/{productId:guid}/variants/{variantId:guid}/enable", async (
            Guid shopId,
            Guid productId,
            Guid variantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new EnableShopProductVariantCommand(shopId, productId, variantId), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null));
        })
        .WithName("Store.Admin.EnableShopProductVariant")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class DisableShopProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/shops/{shopId:guid}/products/{productId:guid}/variants/{variantId:guid}/disable", async (
            Guid shopId,
            Guid productId,
            Guid variantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DisableShopProductVariantCommand(shopId, productId, variantId), ct);
            return Results.Ok(ApiResponseHelper.Success<object?>(null));
        })
        .WithName("Store.Admin.DisableShopProductVariant")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class RemoveShopProductVariantEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/shops/{shopId:guid}/products/{productId:guid}/variants/{variantId:guid}", async (
            Guid shopId,
            Guid productId,
            Guid variantId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RemoveShopProductVariantCommand(shopId, productId, variantId), ct);
            return Results.NoContent();
        })
        .WithName("Store.Admin.RemoveShopProductVariant")
        .WithTags("Store.ShopProducts")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public class GetShopProductVariantBackfillAuditEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/shop-product-variants/backfill/audit", async (
            Guid? shopId,
            Guid? productId,
            int? detailLimit,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetShopProductVariantBackfillAuditQuery(
                shopId,
                productId,
                detailLimit.GetValueOrDefault(100));

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.GetShopProductVariantBackfillAudit")
        .WithTags("Store.ShopProductVariants.Backfill")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ShopProductVariantBackfillAuditDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public class BackfillShopProductVariantsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/shop-product-variants/backfill", async (
            BackfillShopProductVariantsRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new BackfillShopProductVariantsCommand(
                request.ShopId,
                request.ProductId,
                request.DryRun ?? true,
                request.DetailLimit ?? 100);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.Admin.BackfillShopProductVariants")
        .WithTags("Store.ShopProductVariants.Backfill")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<ShopProductVariantBackfillResultDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

public sealed record AddProductToShopRequest(Guid ProductId, long Price, long DiscountedPrice, string? Description);

public sealed record UpdateShopProductRequest(long Price, long DiscountedPrice, string? Description);

public sealed record UpsertShopProductVariantRequest(
    long PriceMinor,
    long? DiscountedPriceMinor,
    bool IsActive);

public sealed record BackfillShopProductVariantsRequest(
    Guid? ShopId,
    Guid? ProductId,
    bool? DryRun,
    int? DetailLimit);
