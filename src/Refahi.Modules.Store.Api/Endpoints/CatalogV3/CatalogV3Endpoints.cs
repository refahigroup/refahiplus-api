using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products.V3;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.CatalogV3;

internal static class V3Actor
{
    public static Guid Id(HttpContext http) =>
        Guid.TryParse(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
            out var id
        )
            ? id
            : Guid.Empty;
}

public sealed record ProductV3Request(
    Guid SupplierId,
    int CategoryId,
    short EligibilityChannel,
    short ProductType,
    short SalesModel,
    short FulfillmentMethod,
    string Title,
    string Slug,
    string? Description
);

public sealed record ProductContentV3Request(string Title, string? Description);

public sealed record ProductActivationV3Request(short EligibilityChannel = 0);

public sealed record ProductVariantStructuralV3Request(
    IReadOnlyList<VariantCombinationV3Input> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Refahi.Modules.Store.Domain.Enums.VariantCapacityType CapacityType =
        Refahi.Modules.Store.Domain.Enums.VariantCapacityType.Unlimited,
    int? Capacity = null
);

public sealed record ProductSessionCreateV3Request(
    string Date,
    string StartTime,
    string EndTime,
    int Capacity,
    string? Title
);

public sealed record ProductSessionUpdateV3Request(int Capacity, string? Title, bool IsActive);

public sealed record OfferRequest(
    Guid ProductId,
    Guid ShopId,
    Guid? ProductVariantId,
    Guid? ProductSessionId,
    long OriginalPriceMinor,
    decimal DiscountPercent,
    DateTimeOffset StartDateUtc,
    DateTimeOffset? EndDateUtc
);

public sealed record OfferUpdateRequest(
    long OriginalPriceMinor,
    decimal DiscountPercent,
    DateTimeOffset StartDateUtc,
    DateTimeOffset? EndDateUtc,
    uint ExpectedVersion
);

public sealed record VersionRequest(uint ExpectedVersion);

public sealed class PublicCatalogV3Endpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;
        const string tag = "Store.PublicCatalog.V3";
        routes
            .MapGet(
                "/v3/{moduleSlug}/products",
                async (
                    string moduleSlug,
                    string? q,
                    int? categoryId,
                    Guid? shopId,
                    string? shopSlug,
                    string? salesModel,
                    long? minPriceMinor,
                    long? maxPriceMinor,
                    string? sort,
                    int? pageNumber,
                    int? pageSize,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    var result = await mediator.Send(
                        new GetPublicProductCatalogV3Query(
                            moduleSlug,
                            q,
                            categoryId,
                            shopId,
                            shopSlug,
                            salesModel,
                            minPriceMinor,
                            maxPriceMinor,
                            sort ?? "newest",
                            pageNumber ?? 1,
                            pageSize ?? 30
                        ),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("ماژول فروشگاه یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(
                            ApiResponseHelper.SuccessPaginated(
                                result.Items,
                                result.Page,
                                result.PageSize,
                                result.Total
                            )
                        );
                }
            )
            .WithName("Store.V3.PublicProductCatalog")
            .WithTags(tag)
            .Produces<PaginatedResponse<PublicProductCatalogItemV3Dto>>(200)
            .Produces<ApiErrorResponse>(404);

        routes
            .MapGet(
                "/v3/{moduleSlug}/products/{productSlug}",
                async (
                    string moduleSlug,
                    string productSlug,
                    Guid? shopId,
                    string? shopSlug,
                    Guid? offerId,
                    Guid? variantId,
                    Guid? sessionId,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                    await PublicDetail(
                        moduleSlug,
                        productSlug,
                        shopId,
                        shopSlug,
                        offerId,
                        variantId,
                        sessionId,
                        mediator,
                        ct
                    )
            )
            .WithName("Store.V3.PublicProductDetail")
            .WithTags(tag)
            .Produces<ApiResponse<PublicProductDetailV3Dto>>(200)
            .Produces<ApiErrorResponse>(404);

        routes
            .MapGet(
                "/v3/{moduleSlug}/{shopSlug}/products/{productSlug}",
                async (
                    string moduleSlug,
                    string shopSlug,
                    string productSlug,
                    Guid? offerId,
                    Guid? variantId,
                    Guid? sessionId,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                    await PublicDetail(
                        moduleSlug,
                        productSlug,
                        null,
                        shopSlug,
                        offerId,
                        variantId,
                        sessionId,
                        mediator,
                        ct
                    )
            )
            .WithName("Store.V3.PublicShopProductDetail")
            .WithTags(tag)
            .Produces<ApiResponse<PublicProductDetailV3Dto>>(200)
            .Produces<ApiErrorResponse>(404);
    }

    private static async Task<IResult> PublicDetail(
        string moduleSlug,
        string productSlug,
        Guid? shopId,
        string? shopSlug,
        Guid? offerId,
        Guid? variantId,
        Guid? sessionId,
        IMediator mediator,
        CancellationToken ct
    )
    {
        var result = await mediator.Send(
            new GetPublicProductDetailV3Query(
                moduleSlug,
                productSlug,
                shopId,
                shopSlug,
                offerId,
                variantId,
                sessionId
            ),
            ct
        );
        return result is null
            ? Results.NotFound(
                ApiResponseHelper.Error("محصول یا پیشنهاد معتبر یافت نشد", statusCode: 404)
            )
            : Results.Ok(ApiResponseHelper.Success(result));
    }
}

public sealed class ProductV3WriteEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r)
            return;
        MapRole(r, "admin", true, "AdminOnly");
        MapRole(r, "vendor", false, "VendorOrAdmin");
    }

    private static void MapRole(IEndpointRouteBuilder r, string role, bool admin, string policy)
    {
        var tag = $"Store.Products.V3.{role}";
        r.MapGet(
                $"/v3/{role}/products",
                async (
                    Guid supplierId,
                    int? categoryId,
                    int? page,
                    int? pageSize,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                V3Actor.Id(h),
                                supplierId,
                                null,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Forbid();
                    var x = await m.Send(
                        new ListProductsV3Query(
                            supplierId,
                            categoryId,
                            true,
                            page ?? 1,
                            pageSize ?? 30
                        ),
                        ct
                    );
                    return Results.Ok(
                        ApiResponseHelper.SuccessPaginated(x.Items, x.Page, x.PageSize, x.Total)
                    );
                }
            )
            .WithName($"Store.V3.{role}.ListProducts")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/v3/{role}/products/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(new GetProductV3Query(id, true), ct);
                    if (x is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                V3Actor.Id(h),
                                x.SupplierId,
                                null,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Forbid();
                    return Results.Ok(ApiResponseHelper.Success(x));
                }
            )
            .WithName($"Store.V3.{role}.GetProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/v3/{role}/products/{{id:guid}}/detail",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var product = await m.Send(new GetProductV3Query(id, true), ct);
                    if (product is null)
                        return Results.NotFound(
                            ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)
                        );
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                V3Actor.Id(h),
                                product.SupplierId,
                                null,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Json(
                            ApiResponseHelper.Error(
                                "دسترسی مدیریت محصول وجود ندارد",
                                statusCode: 403
                            ),
                            statusCode: 403
                        );
                    var detail = await m.Send(new GetProductV3ManagementDetailQuery(id, role), ct);
                    return detail is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("جزئیات محصول یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(detail));
                }
            )
            .WithName($"Store.V3.{role}.GetProductDetail")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductV3ManagementDetailDto>>(200)
            .Produces<ApiErrorResponse>(403)
            .Produces<ApiErrorResponse>(404);
        r.MapPost(
                $"/v3/{role}/products/{{id:guid}}/variants",
                async (
                    Guid id,
                    ProductVariantStructuralV3Request b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new CreateProductVariantV3Command(
                            V3Actor.Id(h),
                            admin,
                            id,
                            b.Combinations,
                            b.ImageUrl,
                            b.StockCount,
                            b.Sku,
                            b.FromDate,
                            b.ToDate,
                            b.CapacityType,
                            b.Capacity
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/v3/{role}/products/{id}/variants/{x.Id}",
                        ApiResponseHelper.Success(x, "تنوع محصول ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.V3.{role}.CreateProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductVariantV3Dto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPut(
                $"/v3/{role}/products/{{id:guid}}/variants/{{variantId:guid}}",
                async (
                    Guid id,
                    Guid variantId,
                    ProductVariantStructuralV3Request b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateProductVariantV3Command(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    variantId,
                                    b.Combinations,
                                    b.ImageUrl,
                                    b.StockCount,
                                    b.Sku,
                                    b.FromDate,
                                    b.ToDate,
                                    b.CapacityType,
                                    b.Capacity
                                ),
                                ct
                            ),
                            "تنوع محصول ویرایش شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.UpdateProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductVariantV3Dto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapDelete(
                $"/v3/{role}/products/{{id:guid}}/variants/{{variantId:guid}}",
                async (Guid id, Guid variantId, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    await m.Send(
                        new DeleteProductVariantV3Command(V3Actor.Id(h), admin, id, variantId),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success("تنوع محصول حذف شد"));
                }
            )
            .WithName($"Store.V3.{role}.DeleteProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<string>>(200)
            .Produces<ApiErrorResponse>(403)
            .Produces<ApiErrorResponse>(404);
        r.MapPost(
                $"/v3/{role}/products/{{id:guid}}/sessions",
                async (
                    Guid id,
                    ProductSessionCreateV3Request b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new CreateProductSessionV3Command(
                            V3Actor.Id(h),
                            admin,
                            id,
                            b.Date,
                            b.StartTime,
                            b.EndTime,
                            b.Capacity,
                            b.Title
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/v3/{role}/products/{id}/sessions/{x.Id}",
                        ApiResponseHelper.Success(x, "سانس ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.V3.{role}.CreateProductSession")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductSessionV3Dto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPut(
                $"/v3/{role}/products/{{id:guid}}/sessions/{{sessionId:guid}}",
                async (
                    Guid id,
                    Guid sessionId,
                    ProductSessionUpdateV3Request b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateProductSessionV3Command(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    sessionId,
                                    b.Capacity,
                                    b.Title,
                                    b.IsActive
                                ),
                                ct
                            ),
                            "سانس ویرایش شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.UpdateProductSession")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductSessionV3Dto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPost(
                $"/v3/{role}/products",
                async (ProductV3Request body, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(
                        new CreateProductV3Command(
                            V3Actor.Id(h),
                            admin,
                            body.SupplierId,
                            body.CategoryId,
                            body.EligibilityChannel,
                            body.ProductType,
                            body.SalesModel,
                            body.FulfillmentMethod,
                            body.Title,
                            body.Slug,
                            body.Description
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/v3/catalog/products/{x.Id}",
                        ApiResponseHelper.Success(x, "محصول ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.V3.{role}.CreateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductV3Dto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPut(
                $"/v3/{role}/products/{{id:guid}}",
                async (
                    Guid id,
                    ProductContentV3Request body,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateProductV3Command(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    body.Title,
                                    body.Description
                                ),
                                ct
                            ),
                            "محصول ویرایش شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.UpdateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductV3Dto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPost(
                $"/v3/{role}/products/{{id:guid}}/activate",
                async (
                    Guid id,
                    ProductActivationV3Request body,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetProductV3ActivationCommand(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    body.EligibilityChannel,
                                    true
                                ),
                                ct
                            ),
                            "محصول فعال شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.ActivateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/v3/{role}/products/{{id:guid}}/deactivate",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetProductV3ActivationCommand(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    1,
                                    false
                                ),
                                ct
                            ),
                            "محصول غیرفعال شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.DeactivateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapDelete(
                $"/v3/{role}/products/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    await m.Send(new DeleteProductV3Command(V3Actor.Id(h), admin, id), ct);
                    return Results.Ok(ApiResponseHelper.Success("محصول حذف شد"));
                }
            )
            .WithName($"Store.V3.{role}.DeleteProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
    }
}

public sealed class OfferV3WriteEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r)
            return;
        MapRole(r, "admin", true, "AdminOnly");
        MapRole(r, "vendor", false, "VendorOrAdmin");
    }

    private static void MapRole(IEndpointRouteBuilder r, string role, bool admin, string policy)
    {
        var tag = $"Store.Offers.V3.{role}";
        r.MapGet(
                $"/v3/{role}/offers",
                async (
                    Guid productId,
                    Guid? shopId,
                    int? page,
                    int? pageSize,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var p = await m.Send(new GetProductV3Query(productId, true), ct);
                    if (p is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                V3Actor.Id(h),
                                p.SupplierId,
                                shopId,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Forbid();
                    var x = await m.Send(
                        new ListOffersQuery(
                            productId,
                            shopId,
                            true,
                            null,
                            page ?? 1,
                            pageSize ?? 30
                        ),
                        ct
                    );
                    return Results.Ok(
                        ApiResponseHelper.SuccessPaginated(x.Items, x.Page, x.PageSize, x.Total)
                    );
                }
            )
            .WithName($"Store.V3.{role}.ListOffers")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/v3/{role}/offers/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(new GetOfferQuery(id, true), ct);
                    if (x is null)
                        return Results.NotFound();
                    var p = await m.Send(new GetProductV3Query(x.ProductId, true), ct);
                    if (p is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                V3Actor.Id(h),
                                p.SupplierId,
                                x.ShopId,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Forbid();
                    return Results.Ok(ApiResponseHelper.Success(x));
                }
            )
            .WithName($"Store.V3.{role}.GetOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/v3/{role}/offers",
                async (OfferRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(
                        new CreateOfferCommand(
                            V3Actor.Id(h),
                            admin,
                            b.ProductId,
                            b.ShopId,
                            b.ProductVariantId,
                            b.ProductSessionId,
                            b.OriginalPriceMinor,
                            b.DiscountPercent,
                            b.StartDateUtc,
                            b.EndDateUtc
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/v3/catalog/offers/{x.Id}",
                        ApiResponseHelper.Success(x, "پیشنهاد ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.V3.{role}.CreateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<OfferDto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPut(
                $"/v3/{role}/offers/{{id:guid}}",
                async (
                    Guid id,
                    OfferUpdateRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateOfferCommand(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    b.OriginalPriceMinor,
                                    b.DiscountPercent,
                                    b.StartDateUtc,
                                    b.EndDateUtc,
                                    b.ExpectedVersion
                                ),
                                ct
                            ),
                            "پیشنهاد ویرایش شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.UpdateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/v3/{role}/offers/{{id:guid}}/activate",
                async (
                    Guid id,
                    VersionRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetOfferActivationCommand(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    true,
                                    b.ExpectedVersion
                                ),
                                ct
                            ),
                            "پیشنهاد فعال شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.ActivateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/v3/{role}/offers/{{id:guid}}/deactivate",
                async (
                    Guid id,
                    VersionRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetOfferActivationCommand(
                                    V3Actor.Id(h),
                                    admin,
                                    id,
                                    false,
                                    b.ExpectedVersion
                                ),
                                ct
                            ),
                            "پیشنهاد غیرفعال شد"
                        )
                    )
            )
            .WithName($"Store.V3.{role}.DeactivateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapDelete(
                $"/v3/{role}/offers/{{id:guid}}",
                async (
                    Guid id,
                    uint expectedVersion,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    await m.Send(
                        new DeleteOfferCommand(V3Actor.Id(h), admin, id, expectedVersion),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success("پیشنهاد حذف شد"));
                }
            )
            .WithName($"Store.V3.{role}.DeleteOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
    }
}

public sealed class CatalogV3ReadEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r)
            return;
        r.MapGet(
                "/v3/catalog/products",
                async (
                    Guid? supplierId,
                    int? categoryId,
                    int? page,
                    int? pageSize,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new ListProductsV3Query(
                            supplierId,
                            categoryId,
                            false,
                            page ?? 1,
                            pageSize ?? 30
                        ),
                        ct
                    );
                    return Results.Ok(
                        ApiResponseHelper.SuccessPaginated(x.Items, x.Page, x.PageSize, x.Total)
                    );
                }
            )
            .WithName("Store.V3.Catalog.ListProducts")
            .WithTags("Store.Catalog.V3")
            .Produces<PaginatedResponse<ProductV3Dto>>(200);
        r.MapGet(
                "/v3/catalog/products/{id:guid}",
                async (Guid id, IMediator m, CancellationToken ct) =>
                    await m.Send(new GetProductV3Query(id, false), ct) is { } x
                        ? Results.Ok(ApiResponseHelper.Success(x))
                        : Results.NotFound(
                            ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)
                        )
            )
            .WithName("Store.V3.Catalog.GetProduct")
            .WithTags("Store.Catalog.V3");
        r.MapGet(
                "/v3/catalog/offers",
                async (
                    Guid? productId,
                    Guid? shopId,
                    int? page,
                    int? pageSize,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new ListOffersQuery(
                            productId,
                            shopId,
                            false,
                            DateTimeOffset.UtcNow,
                            page ?? 1,
                            pageSize ?? 30
                        ),
                        ct
                    );
                    return Results.Ok(
                        ApiResponseHelper.SuccessPaginated(x.Items, x.Page, x.PageSize, x.Total)
                    );
                }
            )
            .WithName("Store.V3.Catalog.ListOffers")
            .WithTags("Store.Catalog.V3")
            .Produces<PaginatedResponse<OfferDto>>(200);
        r.MapGet(
                "/v3/catalog/offers/{id:guid}",
                async (Guid id, IMediator m, CancellationToken ct) =>
                    await m.Send(new GetOfferQuery(id, false, DateTimeOffset.UtcNow), ct) is { } x
                        ? Results.Ok(ApiResponseHelper.Success(x))
                        : Results.NotFound(
                            ApiResponseHelper.Error("پیشنهاد یافت نشد", statusCode: 404)
                        )
            )
            .WithName("Store.V3.Catalog.GetOffer")
            .WithTags("Store.Catalog.V3");
    }
}
