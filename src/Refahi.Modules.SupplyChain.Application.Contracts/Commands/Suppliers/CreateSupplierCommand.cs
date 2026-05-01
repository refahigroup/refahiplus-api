using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;

public sealed record CreateSupplierCommand(
    short Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? BrandName,
    string? LogoUrl,
    string? NationalId,
    string? EconomicCode,
    int? ProvinceId,
    int? CityId,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? MobileNumber,
    string? PhoneNumber,
    string? RepresentativeName,
    string? RepresentativePhone
) : IRequest<CreateSupplierResponse>;

public sealed record CreateSupplierResponse(Guid Id);
