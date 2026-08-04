using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreateWallet;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.VendorAccess;

public sealed class GetStoreVendorContextsHandler(
    IMediator mediator, IShopRepository shops, IPathService paths,
    Microsoft.Extensions.Logging.ILogger<GetStoreVendorContextsHandler> logger)
    : IRequestHandler<GetStoreVendorContextsQuery, IReadOnlyList<StoreVendorContextDto>>
{
    public async Task<IReadOnlyList<StoreVendorContextDto>> Handle(
        GetStoreVendorContextsQuery request, CancellationToken ct)
    {
        var grants = await mediator.Send(
            new GetActiveAuthorizationGrantsQuery(request.UserId, StoreGrantCodec.Issuer), ct);
        var parsed = new List<ParsedStoreGrant>();
        foreach (var grant in grants)
        {
            if (StoreGrantCodec.TryParse(grant.Value, out var value) && value is not null)
                parsed.Add(value);
            else
                logger.LogWarning("Invalid Store grant ignored. GrantId={GrantId}, UserId={UserId}",
                    grant.Id, grant.UserId);
        }

        var vendorIds = parsed.Where(x => x.ResourceType == "vendor").Select(x => x.ResourceId).ToHashSet();
        foreach (var shopId in parsed.Where(x => x.ResourceType == "shop").Select(x => x.ResourceId).Distinct())
        {
            var shop = await shops.GetByIdAsync(shopId, ct);
            if (shop is not null)
            {
                vendorIds.Add(shop.SupplierId);
            }
        }

        var result = new List<StoreVendorContextDto>();
        foreach (var vendorId in vendorIds)
        {
            var vendor = await mediator.Send(new GetSupplierByIdQuery(vendorId), ct);
            if (vendor is null || vendor.Status != 3) continue;

            var vendorRoles = parsed.Where(x => x.ResourceType == "vendor" && x.ResourceId == vendorId)
                .Select(x => x.Role).Distinct().ToArray();
            var allVendorShops = await shops.GetBySupplierIdAsync(vendorId, ct);
            var shopDtos = new List<VendorShopAccessDto>();

            foreach (var shop in allVendorShops.Where(x => x.Status == ShopStatus.Active))
            {
                var shopRoles = parsed.Where(x => x.ResourceType == "shop" && x.ResourceId == shop.Id)
                    .Select(x => x.Role).Concat(vendorRoles).Distinct().ToArray();
                if (shopRoles.Length == 0) continue;
                shopDtos.Add(new(
                    shop.Id, shop.Name, shop.Status.ToString(), shop.ShopType.ToString(),
                    shop.LogoUrl is null ? null : paths.MakeAbsoluteMediaUrl(shop.LogoUrl),
                    shopRoles, PermissionsFor(shopRoles, shop.Id, vendorId)));
            }

            if (vendorRoles.Length == 0 && shopDtos.Count == 0) continue;
            var name = vendor.BrandName ?? vendor.CompanyName ??
                string.Join(' ', new[] { vendor.FirstName, vendor.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            result.Add(new(vendorId, name, vendorRoles,
                PermissionsFor(vendorRoles, null, vendorId), shopDtos));
        }
        return result;
    }

    internal static IReadOnlyList<string> PermissionsFor(
        IEnumerable<string> roles, Guid? shopId, Guid vendorId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            switch (role)
            {
                case StoreAccessRoles.VendorOwner:
                    result.UnionWith(new[] { StorePermissions.ViewOrders, StorePermissions.ManageOrders,
                        StorePermissions.CreateInPersonOrder, StorePermissions.EditVendorProfile,
                        StorePermissions.EditShopProfile, StorePermissions.ViewIncomeWallet,
                        StorePermissions.RefundInPersonOrder });
                    break;
                case StoreAccessRoles.VendorSupervisor:
                case StoreAccessRoles.ShopSupervisor:
                    result.UnionWith(new[] { StorePermissions.ViewOrders, StorePermissions.ManageOrders,
                        StorePermissions.CreateInPersonOrder });
                    if (role == StoreAccessRoles.ShopSupervisor) result.Add(StorePermissions.EditShopProfile);
                    break;
                case StoreAccessRoles.ShopCashier:
                    result.Add(StorePermissions.CreateInPersonOrder);
                    result.Add(StorePermissions.ViewOwnOrders);
                    break;
            }
        }
        return result.ToArray();
    }
}

public sealed class AuthorizeStoreResourceHandler(IMediator mediator)
    : IRequestHandler<AuthorizeStoreResourceQuery, bool>
{
    public async Task<bool> Handle(AuthorizeStoreResourceQuery request, CancellationToken ct)
    {
        var contexts = await mediator.Send(new GetStoreVendorContextsQuery(request.UserId), ct);
        var vendor = contexts.SingleOrDefault(x => x.VendorId == request.VendorId);
        if (vendor is null) return false;
        var permissions = request.ShopId.HasValue
            ? vendor.Shops.SingleOrDefault(x => x.Id == request.ShopId.Value)?.Permissions
            : vendor.Permissions;
        return permissions?.Contains(request.Permission, StringComparer.OrdinalIgnoreCase) == true;
    }
}

public sealed class GetStoreAccessAssignmentsHandler(IMediator mediator, IShopRepository shops)
    : IRequestHandler<GetStoreAccessAssignmentsQuery, IReadOnlyList<StoreAccessAssignmentDto>>
{
    public async Task<IReadOnlyList<StoreAccessAssignmentDto>> Handle(
        GetStoreAccessAssignmentsQuery request, CancellationToken ct)
    {
        var grants = await mediator.Send(new GetAuthorizationGrantsByIssuerQuery(StoreGrantCodec.Issuer), ct);
        var rows = new List<(AuthorizationGrantDto Grant, ParsedStoreGrant Parsed)>();
        foreach (var grant in grants)
        {
            if (!StoreGrantCodec.TryParse(grant.Value, out var parsed) || parsed is null) continue;
            var belongs = parsed.ResourceType == "vendor" && parsed.ResourceId == request.VendorId;
            if (parsed.ResourceType == "shop")
                belongs = (await shops.GetByIdAsync(parsed.ResourceId, ct))?.SupplierId == request.VendorId;
            if (belongs) rows.Add((grant, parsed));
        }
        var users = await mediator.Send(new GetOrderUserSummariesQuery(rows.Select(x => x.Grant.UserId).Distinct().ToArray()), ct);
        var byId = users.ToDictionary(x => x.UserId);
        return rows.Select(x =>
        {
            byId.TryGetValue(x.Grant.UserId, out var user);
            var displayName = string.Join(' ', new[] { user?.FirstName, user?.LastName }.Where(v => !string.IsNullOrWhiteSpace(v)));
            return new StoreAccessAssignmentDto(x.Grant.Id, x.Grant.UserId, user?.MobileNumber,
                string.IsNullOrWhiteSpace(displayName) ? null : displayName, request.VendorId,
                x.Parsed.ResourceType, x.Parsed.ResourceId, x.Parsed.Role, x.Grant.IsActive, x.Grant.CreatedAt);
        }).ToList();
    }
}

public sealed class GetStoreAccessSummariesHandler(IMediator mediator, IShopRepository shops)
    : IRequestHandler<GetStoreAccessSummariesQuery, IReadOnlyList<StoreAccessSummaryDto>>
{
    public async Task<IReadOnlyList<StoreAccessSummaryDto>> Handle(
        GetStoreAccessSummariesQuery request, CancellationToken ct)
    {
        var vendorIds = request.VendorIds.Distinct().ToHashSet();
        if (vendorIds.Count == 0) return [];

        var grants = await mediator.Send(new GetAuthorizationGrantsByIssuerQuery(StoreGrantCodec.Issuer), ct);
        var activeAssignments = new List<(Guid VendorId, Guid UserId, string Role)>();
        var shopVendors = new Dictionary<Guid, Guid?>();

        foreach (var grant in grants.Where(x => x.IsActive))
        {
            if (!StoreGrantCodec.TryParse(grant.Value, out var parsed) || parsed is null) continue;

            Guid? vendorId = null;
            if (parsed.ResourceType == "vendor")
            {
                vendorId = parsed.ResourceId;
            }
            else if (parsed.ResourceType == "shop")
            {
                if (!shopVendors.TryGetValue(parsed.ResourceId, out vendorId))
                {
                    vendorId = (await shops.GetByIdAsync(parsed.ResourceId, ct))?.SupplierId;
                    shopVendors[parsed.ResourceId] = vendorId;
                }
            }

            if (vendorId.HasValue && vendorIds.Contains(vendorId.Value))
                activeAssignments.Add((vendorId.Value, grant.UserId, parsed.Role));
        }

        return vendorIds.Select(vendorId =>
        {
            var assignments = activeAssignments.Where(x => x.VendorId == vendorId).ToArray();
            var activeUserCount = assignments.Select(x => x.UserId).Distinct().Count();
            var activeOwnerCount = assignments
                .Where(x => x.Role == StoreAccessRoles.VendorOwner)
                .Select(x => x.UserId)
                .Distinct()
                .Count();
            return new StoreAccessSummaryDto(vendorId, activeUserCount, activeOwnerCount);
        }).ToArray();
    }
}

public sealed class CreateStoreAccessAssignmentHandler(IMediator mediator, IShopRepository shops)
    : IRequestHandler<CreateStoreAccessAssignmentCommand, StoreAccessAssignmentDto>
{
    public async Task<StoreAccessAssignmentDto> Handle(CreateStoreAccessAssignmentCommand request, CancellationToken ct)
    {
        await ValidateResource(request.VendorId, request.ResourceType, request.ResourceId, request.Role, mediator, shops, ct);
        await mediator.Send(new CreateWalletCommand(
            request.VendorId, WalletTypeCodes.Provider, "IRR"), ct);
        var currentAssignments = await mediator.Send(new GetStoreAccessAssignmentsQuery(request.VendorId), ct);
        foreach (var current in currentAssignments.Where(x =>
                     x.UserId == request.UserId && x.IsActive &&
                     x.ResourceType.Equals(request.ResourceType, StringComparison.OrdinalIgnoreCase) &&
                     x.ResourceId == request.ResourceId &&
                     !x.Role.Equals(request.Role, StringComparison.Ordinal)))
            await mediator.Send(new RevokeAuthorizationGrantCommand(current.Id, request.ActorId), ct);

        var value = StoreGrantCodec.Encode(request.ResourceType, request.ResourceId, request.Role);
        var grant = await mediator.Send(new UpsertAuthorizationGrantCommand(
            request.UserId, StoreGrantCodec.Issuer, value, StoreGrantCodec.EmittedRole, request.ActorId), ct);
        return new(grant.Id, grant.UserId, null, null, request.VendorId,
            request.ResourceType.ToLowerInvariant(), request.ResourceId, request.Role, true, grant.CreatedAt);
    }

    internal static async Task ValidateResource(Guid vendorId, string resourceType, Guid resourceId, string role,
        IMediator mediator, IShopRepository shops, CancellationToken ct)
    {
        _ = StoreGrantCodec.Encode(resourceType, resourceId, role);
        var vendor = await mediator.Send(new GetSupplierByIdQuery(vendorId), ct);
        if (vendor is null || vendor.Status != 3) throw new InvalidOperationException("Vendor تاییدشده یافت نشد");
        if (resourceType.Equals("vendor", StringComparison.OrdinalIgnoreCase) && resourceId != vendorId)
            throw new InvalidOperationException("شناسه منبع Vendor نامعتبر است");
        if (resourceType.Equals("shop", StringComparison.OrdinalIgnoreCase) &&
            (await shops.GetByIdAsync(resourceId, ct))?.SupplierId != vendorId)
            throw new InvalidOperationException("فروشگاه متعلق به Vendor نیست");
    }
}

public sealed class UpdateStoreAccessAssignmentHandler(IMediator mediator, IShopRepository shops)
    : IRequestHandler<UpdateStoreAccessAssignmentCommand, StoreAccessAssignmentDto?>
{
    public async Task<StoreAccessAssignmentDto?> Handle(UpdateStoreAccessAssignmentCommand request, CancellationToken ct)
    {
        var assignments = await mediator.Send(new GetStoreAccessAssignmentsQuery(request.VendorId), ct);
        var current = assignments.SingleOrDefault(x => x.Id == request.AssignmentId);
        if (current is null) return null;
        if (!request.IsActive)
        {
            await mediator.Send(new RevokeAuthorizationGrantCommand(current.Id, request.ActorId), ct);
            return current with { IsActive = false };
        }
        await CreateStoreAccessAssignmentHandler.ValidateResource(current.VendorId, current.ResourceType,
            current.ResourceId, request.Role, mediator, shops, ct);
        if (!string.Equals(current.Role, request.Role, StringComparison.Ordinal))
        {
            await mediator.Send(new RevokeAuthorizationGrantCommand(current.Id, request.ActorId), ct);
            return await mediator.Send(new CreateStoreAccessAssignmentCommand(current.VendorId, current.UserId,
                current.ResourceType, current.ResourceId, request.Role, request.ActorId), ct);
        }
        return await mediator.Send(new CreateStoreAccessAssignmentCommand(current.VendorId, current.UserId,
            current.ResourceType, current.ResourceId, current.Role, request.ActorId), ct);
    }
}

public sealed class RevokeStoreAccessAssignmentHandler(IMediator mediator)
    : IRequestHandler<RevokeStoreAccessAssignmentCommand, bool>
{
    public async Task<bool> Handle(RevokeStoreAccessAssignmentCommand request, CancellationToken ct)
    {
        var assignment = (await mediator.Send(new GetStoreAccessAssignmentsQuery(request.VendorId), ct))
            .SingleOrDefault(x => x.Id == request.AssignmentId);
        return assignment is not null && await mediator.Send(
            new RevokeAuthorizationGrantCommand(request.AssignmentId, request.ActorId), ct);
    }
}
