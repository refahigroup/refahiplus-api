using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Shops;

public sealed record UpdateShopCommand(
    Guid Id,
    string Name,
    string? Description,
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
    string? LogoUrl,
    string? CoverImageUrl
) : IRequest<UpdateShopResponse>;

public sealed record UpdateShopResponse(Guid Id, string Name);
