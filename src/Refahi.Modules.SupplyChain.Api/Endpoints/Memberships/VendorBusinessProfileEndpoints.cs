using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Memberships;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Memberships;

public sealed record UpdateVendorSupplierProfileRequest(
    string? BrandName,
    string? MobileNumber,
    string? PhoneNumber,
    string? RepresentativeName,
    string? RepresentativePhone
);

public sealed record UpdateVendorShopProfileRequest(
    string Name,
    string? ContactPhone,
    string? Address,
    string? LogoUrl,
    string? CoverImageUrl
);

public sealed class VendorBusinessProfileEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;
        routes
            .MapGet(
                "/vendor/suppliers/{supplierId:guid}/profile",
                async (
                    Guid supplierId,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new GetVendorBusinessProfileQuery(userId, supplierId),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error("پروفایل کسب‌وکار یافت نشد", statusCode: 404)
                        )
                        : Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("SupplyChain.Vendor.Profile.Get")
            .WithTags("SupplyChain.Vendor")
            .RequireAuthorization("VendorOnly");

        routes
            .MapPut(
                "/vendor/suppliers/{supplierId:guid}/profile",
                async (
                    Guid supplierId,
                    UpdateVendorSupplierProfileRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new UpdateVendorSupplierProfileCommand(
                            userId,
                            supplierId,
                            body.BrandName,
                            body.MobileNumber,
                            body.PhoneNumber,
                            body.RepresentativeName,
                            body.RepresentativePhone
                        ),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error(
                                "دسترسی ویرایش پروفایل وجود ندارد",
                                statusCode: 404
                            )
                        )
                        : Results.Ok(
                            ApiResponseHelper.Success(result, "پروفایل تامین‌کننده بروزرسانی شد")
                        );
                }
            )
            .WithName("SupplyChain.Vendor.Profile.UpdateSupplier")
            .WithTags("SupplyChain.Vendor")
            .RequireAuthorization("VendorOnly");

        routes
            .MapPut(
                "/vendor/suppliers/{supplierId:guid}/shops/{shopId:guid}/profile",
                async (
                    Guid supplierId,
                    Guid shopId,
                    UpdateVendorShopProfileRequest body,
                    ClaimsPrincipal principal,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!TryUserId(principal, out var userId))
                        return Results.Unauthorized();
                    var result = await mediator.Send(
                        new UpdateVendorShopProfileCommand(
                            userId,
                            supplierId,
                            shopId,
                            body.Name,
                            body.ContactPhone,
                            body.Address,
                            body.LogoUrl,
                            body.CoverImageUrl
                        ),
                        ct
                    );
                    return result is null
                        ? Results.NotFound(
                            ApiResponseHelper.Error(
                                "دسترسی ویرایش فروشگاه وجود ندارد",
                                statusCode: 404
                            )
                        )
                        : Results.Ok(
                            ApiResponseHelper.Success(result, "پروفایل فروشگاه بروزرسانی شد")
                        );
                }
            )
            .WithName("SupplyChain.Vendor.Profile.UpdateShop")
            .WithTags("SupplyChain.Vendor")
            .RequireAuthorization("VendorOnly");
    }

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"),
            out userId
        );
}
