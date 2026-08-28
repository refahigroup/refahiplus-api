namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

public sealed record CommerceAddressDto(
    string State,
    string City,
    string Address,
    LocationDto? Location
);


public sealed record LocationDto(
    double Lat,
    double Lng
);