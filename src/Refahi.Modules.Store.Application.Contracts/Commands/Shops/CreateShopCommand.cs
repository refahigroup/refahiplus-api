using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record CreateShopCommand(
    string Name,
    string Slug,
    short ShopType,
    Guid ProviderId,
    int? ProvinceId,
    int? CityId,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? ManagerName,
    string? ManagerPhone,
    string? RepresentativeName,
    string? RepresentativePhone,
    string? ContactPhone,
    string? Description
) : IRequest<CreateShopResponse>;

public sealed record CreateShopResponse(Guid Id, string Name, string Slug, string Status);
