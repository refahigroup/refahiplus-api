using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Modules.SupplyChain.Application.Contracts.Memberships;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

namespace Refahi.Modules.SupplyChain.Application.Features.Memberships;

public sealed class GetVendorBusinessProfileHandler(IMediator mediator)
    : IRequestHandler<GetVendorBusinessProfileQuery, VendorBusinessProfileDto?>
{
    public async Task<VendorBusinessProfileDto?> Handle(
        GetVendorBusinessProfileQuery request,
        CancellationToken ct
    )
    {
        return await BuildForUserAsync(request.UserId, request.SupplierId, mediator, ct);
    }

    internal static async Task<VendorBusinessProfileDto?> BuildForUserAsync(
        Guid userId,
        Guid supplierId,
        IMediator mediator,
        CancellationToken ct
    )
    {
        var context = (
            await mediator.Send(new GetStoreVendorContextsQuery(userId), ct)
        ).SingleOrDefault(x => x.VendorId == supplierId);
        if (context is null)
            return null;
        var profile = await BuildAsync(supplierId, mediator, ct);
        if (profile is null)
            return null;
        var accessibleShopIds = context.Shops.Select(x => x.Id).ToHashSet();
        return profile with
        {
            Shops = profile.Shops.Where(x => accessibleShopIds.Contains(x.Id)).ToArray(),
        };
    }

    internal static async Task<VendorBusinessProfileDto?> BuildAsync(
        Guid supplierId,
        IMediator mediator,
        CancellationToken ct
    )
    {
        var supplier = await mediator.Send(new GetSupplierByIdQuery(supplierId), ct);
        if (supplier is null)
            return null;
        var supplierShops = await mediator.Send(
            new GetVendorShopsBySupplierIdQuery(supplierId),
            ct
        );
        var shops = new List<VendorBusinessShopDto>();
        foreach (var vendorShop in supplierShops)
        {
            var shop = await mediator.Send(new AdminGetShopQuery(vendorShop.Id), ct);
            if (shop is not null)
                shops.Add(
                    new(
                        shop.Id,
                        shop.Name,
                        shop.ContactPhone,
                        shop.Address,
                        shop.LogoUrl,
                        shop.CoverImageUrl
                    )
                );
        }
        var name =
            supplier.BrandName
            ?? supplier.CompanyName
            ?? string.Join(
                ' ',
                new[] { supplier.FirstName, supplier.LastName }.Where(x =>
                    !string.IsNullOrWhiteSpace(x)
                )
            );
        return new(
            supplier.Id,
            name,
            supplier.BrandName,
            supplier.MobileNumber,
            supplier.PhoneNumber,
            supplier.RepresentativeName,
            supplier.RepresentativePhone,
            shops
        );
    }
}

public sealed class UpdateVendorSupplierProfileHandler(IMediator mediator)
    : IRequestHandler<UpdateVendorSupplierProfileCommand, VendorBusinessProfileDto?>
{
    public async Task<VendorBusinessProfileDto?> Handle(
        UpdateVendorSupplierProfileCommand request,
        CancellationToken ct
    )
    {
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.UserId,
                    request.SupplierId,
                    null,
                    StorePermissions.EditVendorProfile
                ),
                ct
            )
        )
            return null;
        var current = await mediator.Send(new GetSupplierByIdQuery(request.SupplierId), ct);
        if (current is null)
            return null;
        await mediator.Send(
            new UpdateSupplierCommand(
                current.Id,
                current.FirstName,
                current.LastName,
                current.CompanyName,
                request.BrandName,
                current.LogoUrl,
                current.NationalId,
                current.EconomicCode,
                current.ProvinceId,
                current.CityId,
                current.Address,
                current.Latitude,
                current.Longitude,
                request.MobileNumber,
                request.PhoneNumber,
                request.RepresentativeName,
                request.RepresentativePhone
            ),
            ct
        );
        return await GetVendorBusinessProfileHandler.BuildForUserAsync(
            request.UserId,
            request.SupplierId,
            mediator,
            ct
        );
    }
}

public sealed class UpdateVendorShopProfileHandler(IMediator mediator)
    : IRequestHandler<UpdateVendorShopProfileCommand, VendorBusinessProfileDto?>
{
    public async Task<VendorBusinessProfileDto?> Handle(
        UpdateVendorShopProfileCommand request,
        CancellationToken ct
    )
    {
        if (
            !await mediator.Send(
                new AuthorizeStoreResourceQuery(
                    request.UserId,
                    request.SupplierId,
                    request.ShopId,
                    StorePermissions.EditShopProfile
                ),
                ct
            )
        )
            return null;
        var shop = await mediator.Send(new AdminGetShopQuery(request.ShopId), ct);
        if (shop is null || shop.SupplierId != request.SupplierId)
            return null;
        await mediator.Send(
            new UpdateShopCommand(
                shop.Id,
                request.Name,
                shop.Description,
                shop.ProvinceId,
                shop.CityId,
                request.Address,
                shop.Latitude,
                shop.Longitude,
                shop.ManagerName,
                shop.ManagerPhone,
                shop.RepresentativeName,
                shop.RepresentativePhone,
                request.ContactPhone,
                request.LogoUrl,
                request.CoverImageUrl,
                null
            ),
            ct
        );
        return await GetVendorBusinessProfileHandler.BuildForUserAsync(
            request.UserId,
            request.SupplierId,
            mediator,
            ct
        );
    }
}
