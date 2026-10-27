namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

public sealed record EntertainmentAddressDto(
    string State,
    string City,
    string Address,
    LocationDto? Location
);


public sealed record LocationDto(
    double Lat,
    double Lng
);