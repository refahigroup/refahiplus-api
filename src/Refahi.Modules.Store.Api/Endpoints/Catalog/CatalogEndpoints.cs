using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Offers;
using Refahi.Modules.Store.Application.Contracts.Products;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Catalog;

internal static class CatalogActor
{
    public static Guid Id(HttpContext http) =>
        Guid.TryParse(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
            out var id
        )
            ? id
            : Guid.Empty;
}

public sealed record ProductRequest(
    Guid SupplierId,
    int CategoryId,
    short EligibilityChannel,
    short ProductType,
    short SalesModel,
    short FulfillmentMethod,
    string Title,
    string Slug,
    string? Description,
    Guid? VoucherSourceId = null
);

public sealed record ProductContentRequest(string Title, string? Description);

public sealed record ProductActivationRequest(short EligibilityChannel = 0);

public sealed record ProductVariantStructuralRequest(
    IReadOnlyList<VariantCombinationInput> Combinations,
    string? ImageUrl,
    int StockCount,
    string? Sku,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Refahi.Modules.Store.Domain.Enums.VariantCapacityType CapacityType =
        Refahi.Modules.Store.Domain.Enums.VariantCapacityType.Unlimited,
    int? Capacity = null,
    Guid? VoucherSourceId = null
);

public sealed record ProductSessionCreateRequest(
    string Date,
    string StartTime,
    string EndTime,
    int Capacity,
    string? Title
);

public sealed record ProductSessionUpdateRequest(int Capacity, string? Title, bool IsActive);

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

public sealed class PublicCatalogEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;
        const string tag = "Store.PublicCatalog";
        routes
            .MapGet(
                "/{moduleSlug}/products",
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
                        new GetPublicProductCatalogQuery(
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
            .WithName("Store.PublicProductCatalog")
            .WithTags(tag)
            .Produces<PaginatedResponse<PublicProductCatalogItemDto>>(200)
            .Produces<ApiErrorResponse>(404);

        routes
            .MapGet(
                "/{moduleSlug}/products/{productSlug}",
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
            .WithName("Store.PublicProductDetail")
            .WithTags(tag)
            .Produces<ApiResponse<PublicProductDetailDto>>(200)
            .Produces<ApiErrorResponse>(404);

        routes
            .MapGet(
                "/{moduleSlug}/shops/{shopSlug}/products/{productSlug}",
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
            .WithName("Store.PublicShopScopedProductDetail")
            .WithTags(tag)
            .Produces<ApiResponse<PublicProductDetailDto>>(200)
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
            new GetPublicProductDetailQuery(
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

public sealed class ProductManagementEndpoints : IEndpoint
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
        var tag = $"Store.Products.{role}";
        r.MapGet(
                $"/{role}/products",
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
                                CatalogActor.Id(h),
                                supplierId,
                                null,
                                StorePermissions.ManageCatalog
                            ),
                            ct
                        )
                    )
                        return Results.Forbid();
                    var x = await m.Send(
                        new ListCatalogProductsQuery(
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
            .WithName($"Store.{role}.ListProducts")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/{role}/products/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(new GetCatalogProductQuery(id, true), ct);
                    if (x is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                CatalogActor.Id(h),
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
            .WithName($"Store.{role}.GetProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/{role}/products/{{id:guid}}/detail",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var product = await m.Send(new GetCatalogProductQuery(id, true), ct);
                    if (product is null)
                        return Results.NotFound(
                            ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)
                        );
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                CatalogActor.Id(h),
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
                    var detail = await m.Send(new GetProductManagementDetailQuery(id, role), ct);
                    return detail is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("جزئیات محصول یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(detail));
                }
            )
            .WithName($"Store.{role}.GetProductDetail")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductManagementDetailDto>>(200)
            .Produces<ApiErrorResponse>(403)
            .Produces<ApiErrorResponse>(404);
        r.MapPost(
                $"/{role}/products/{{id:guid}}/variants",
                async (
                    Guid id,
                    ProductVariantStructuralRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new CreateCatalogProductVariantCommand(
                            CatalogActor.Id(h),
                            admin,
                            id,
                            b.Combinations,
                            b.ImageUrl,
                            b.StockCount,
                            b.Sku,
                            b.FromDate,
                            b.ToDate,
                            b.CapacityType,
                            b.Capacity,
                            b.VoucherSourceId
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/{role}/products/{id}/variants/{x.Id}",
                        ApiResponseHelper.Success(x, "تنوع محصول ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.{role}.CreateProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductVariantStructureDto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPut(
                $"/{role}/products/{{id:guid}}/variants/{{variantId:guid}}",
                async (
                    Guid id,
                    Guid variantId,
                    ProductVariantStructuralRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateCatalogProductVariantCommand(
                                    CatalogActor.Id(h),
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
                                    b.Capacity,
                                    b.VoucherSourceId
                                ),
                                ct
                            ),
                            "تنوع محصول ویرایش شد"
                        )
                    )
            )
            .WithName($"Store.{role}.UpdateProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductVariantStructureDto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapDelete(
                $"/{role}/products/{{id:guid}}/variants/{{variantId:guid}}",
                async (Guid id, Guid variantId, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    await m.Send(
                        new DeleteCatalogProductVariantCommand(CatalogActor.Id(h), admin, id, variantId),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success("تنوع محصول حذف شد"));
                }
            )
            .WithName($"Store.{role}.DeleteProductVariant")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<string>>(200)
            .Produces<ApiErrorResponse>(403)
            .Produces<ApiErrorResponse>(404);
        r.MapPost(
                $"/{role}/products/{{id:guid}}/sessions",
                async (
                    Guid id,
                    ProductSessionCreateRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    var x = await m.Send(
                        new CreateCatalogProductSessionCommand(
                            CatalogActor.Id(h),
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
                        $"/api/store/{role}/products/{id}/sessions/{x.Id}",
                        ApiResponseHelper.Success(x, "سانس ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.{role}.CreateProductSession")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductSessionStructureDto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPut(
                $"/{role}/products/{{id:guid}}/sessions/{{sessionId:guid}}",
                async (
                    Guid id,
                    Guid sessionId,
                    ProductSessionUpdateRequest b,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateCatalogProductSessionCommand(
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.UpdateProductSession")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductSessionStructureDto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(403);
        r.MapPost(
                $"/{role}/products",
                async (ProductRequest body, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(
                        new CreateCatalogProductCommand(
                            CatalogActor.Id(h),
                            admin,
                            body.SupplierId,
                            body.CategoryId,
                            body.EligibilityChannel,
                            body.ProductType,
                            body.SalesModel,
                            body.FulfillmentMethod,
                            body.Title,
                            body.Slug,
                            body.Description,
                            body.VoucherSourceId
                        ),
                        ct
                    );
                    return Results.Created(
                        $"/api/store/catalog/products/{x.Id}",
                        ApiResponseHelper.Success(x, "محصول ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.{role}.CreateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductDto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPut(
                $"/{role}/products/{{id:guid}}",
                async (
                    Guid id,
                    ProductContentRequest body,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new UpdateCatalogProductCommand(
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.UpdateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<ProductDto>>(200)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPost(
                $"/{role}/products/{{id:guid}}/activate",
                async (
                    Guid id,
                    ProductActivationRequest body,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetCatalogProductActivationCommand(
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.ActivateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/{role}/products/{{id:guid}}/deactivate",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                    Results.Ok(
                        ApiResponseHelper.Success(
                            await m.Send(
                                new SetCatalogProductActivationCommand(
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.DeactivateProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapDelete(
                $"/{role}/products/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    await m.Send(new DeleteCatalogProductCommand(CatalogActor.Id(h), admin, id), ct);
                    return Results.Ok(ApiResponseHelper.Success("محصول حذف شد"));
                }
            )
            .WithName($"Store.{role}.DeleteProduct")
            .WithTags(tag)
            .RequireAuthorization(policy);
    }
}

public sealed class OfferManagementEndpoints : IEndpoint
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
        var tag = $"Store.Offers.{role}";
        r.MapGet(
                $"/{role}/offers",
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
                    var p = await m.Send(new GetCatalogProductQuery(productId, true), ct);
                    if (p is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                CatalogActor.Id(h),
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
            .WithName($"Store.{role}.ListOffers")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapGet(
                $"/{role}/offers/{{id:guid}}",
                async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(new GetOfferQuery(id, true), ct);
                    if (x is null)
                        return Results.NotFound();
                    var p = await m.Send(new GetCatalogProductQuery(x.ProductId, true), ct);
                    if (p is null)
                        return Results.NotFound();
                    if (
                        !admin
                        && !await m.Send(
                            new AuthorizeStoreResourceQuery(
                                CatalogActor.Id(h),
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
            .WithName($"Store.{role}.GetOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/{role}/offers",
                async (OfferRequest b, HttpContext h, IMediator m, CancellationToken ct) =>
                {
                    var x = await m.Send(
                        new CreateOfferCommand(
                            CatalogActor.Id(h),
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
                        $"/api/store/catalog/offers/{x.Id}",
                        ApiResponseHelper.Success(x, "پیشنهاد ایجاد شد", 201)
                    );
                }
            )
            .WithName($"Store.{role}.CreateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy)
            .Produces<ApiResponse<OfferDto>>(201)
            .Produces<ApiErrorResponse>(400)
            .Produces(403);
        r.MapPut(
                $"/{role}/offers/{{id:guid}}",
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
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.UpdateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/{role}/offers/{{id:guid}}/activate",
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
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.ActivateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapPost(
                $"/{role}/offers/{{id:guid}}/deactivate",
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
                                    CatalogActor.Id(h),
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
            .WithName($"Store.{role}.DeactivateOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
        r.MapDelete(
                $"/{role}/offers/{{id:guid}}",
                async (
                    Guid id,
                    uint expectedVersion,
                    HttpContext h,
                    IMediator m,
                    CancellationToken ct
                ) =>
                {
                    await m.Send(
                        new DeleteOfferCommand(CatalogActor.Id(h), admin, id, expectedVersion),
                        ct
                    );
                    return Results.Ok(ApiResponseHelper.Success("پیشنهاد حذف شد"));
                }
            )
            .WithName($"Store.{role}.DeleteOffer")
            .WithTags(tag)
            .RequireAuthorization(policy);
    }
}

public sealed class CatalogReadEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r)
            return;
        r.MapGet(
                "/catalog/products",
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
                        new ListCatalogProductsQuery(
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
            .WithName("Store.Catalog.ListProducts")
            .WithTags("Store.Catalog")
            .Produces<PaginatedResponse<ProductDto>>(200);
        r.MapGet(
                "/catalog/products/{id:guid}",
                async (Guid id, IMediator m, CancellationToken ct) =>
                    await m.Send(new GetCatalogProductQuery(id, false), ct) is { } x
                        ? Results.Ok(ApiResponseHelper.Success(x))
                        : Results.NotFound(
                            ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)
                        )
            )
            .WithName("Store.Catalog.GetProduct")
            .WithTags("Store.Catalog");
        r.MapGet(
                "/catalog/offers",
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
            .WithName("Store.Catalog.ListOffers")
            .WithTags("Store.Catalog")
            .Produces<PaginatedResponse<OfferDto>>(200);
        r.MapGet(
                "/catalog/offers/{id:guid}",
                async (Guid id, IMediator m, CancellationToken ct) =>
                    await m.Send(new GetOfferQuery(id, false, DateTimeOffset.UtcNow), ct) is { } x
                        ? Results.Ok(ApiResponseHelper.Success(x))
                        : Results.NotFound(
                            ApiResponseHelper.Error("پیشنهاد یافت نشد", statusCode: 404)
                        )
            )
            .WithName("Store.Catalog.GetOffer")
            .WithTags("Store.Catalog");
    }
}
