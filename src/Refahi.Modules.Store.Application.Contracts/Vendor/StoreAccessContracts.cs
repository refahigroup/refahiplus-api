using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Vendor;

public static class StoreAccessRoles
{
    public const string VendorOwner = nameof(VendorOwner);
    public const string VendorSupervisor = nameof(VendorSupervisor);
    public const string ShopSupervisor = nameof(ShopSupervisor);
    public const string ShopCashier = nameof(ShopCashier);
}

public static class StorePermissions
{
    public const string ViewOrders = nameof(ViewOrders);
    public const string ViewOwnOrders = nameof(ViewOwnOrders);
    public const string ManageOrders = nameof(ManageOrders);
    public const string CreateInPersonOrder = nameof(CreateInPersonOrder);
    public const string EditVendorProfile = nameof(EditVendorProfile);
    public const string EditShopProfile = nameof(EditShopProfile);
    public const string ViewIncomeWallet = nameof(ViewIncomeWallet);
    public const string RefundInPersonOrder = nameof(RefundInPersonOrder);
}

public sealed record StoreAccessAssignmentDto(
    Guid Id, Guid UserId, string? MobileNumber, string? DisplayName,
    Guid VendorId, string ResourceType, Guid ResourceId, string Role,
    bool IsActive, DateTimeOffset CreatedAt);

public sealed record StoreAccessSummaryDto(
    Guid VendorId, int ActiveUserCount, int ActiveOwnerCount);

public sealed record VendorShopAccessDto(
    Guid Id, string Name, string Status, string ShopType, string? LogoUrl,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed record StoreVendorContextDto(
    Guid VendorId, string VendorName, IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions, IReadOnlyList<VendorShopAccessDto> Shops);

public sealed record GetStoreVendorContextsQuery(Guid UserId)
    : IRequest<IReadOnlyList<StoreVendorContextDto>>;

public sealed record AuthorizeStoreResourceQuery(
    Guid UserId, Guid VendorId, Guid? ShopId, string Permission)
    : IRequest<bool>;

public sealed record GetStoreAccessAssignmentsQuery(Guid VendorId)
    : IRequest<IReadOnlyList<StoreAccessAssignmentDto>>;

public sealed record GetStoreAccessSummariesQuery(IReadOnlyCollection<Guid> VendorIds)
    : IRequest<IReadOnlyList<StoreAccessSummaryDto>>;

public sealed record CreateStoreAccessAssignmentCommand(
    Guid VendorId, Guid UserId, string ResourceType, Guid ResourceId, string Role, Guid ActorId)
    : IRequest<StoreAccessAssignmentDto>;

public sealed record UpdateStoreAccessAssignmentCommand(
    Guid VendorId, Guid AssignmentId, string Role, bool IsActive, Guid ActorId)
    : IRequest<StoreAccessAssignmentDto?>;

public sealed record RevokeStoreAccessAssignmentCommand(Guid VendorId, Guid AssignmentId, Guid ActorId)
    : IRequest<bool>;
