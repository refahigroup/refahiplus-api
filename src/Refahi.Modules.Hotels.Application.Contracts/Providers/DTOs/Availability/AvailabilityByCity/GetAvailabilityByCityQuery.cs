using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

public sealed record GetAvailabilityByCityQuery(
    int CityId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int? Adults,
    int? Children,
    int? AvailableRooms,
    long? MinPrice,
    long? MaxPrice,
    int[]? Stars,
    string[]? Accommodations
) : IRequest<GetAvailabilityByCityDto>;
