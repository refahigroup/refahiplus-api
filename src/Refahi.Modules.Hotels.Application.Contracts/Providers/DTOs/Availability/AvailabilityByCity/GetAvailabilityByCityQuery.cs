using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

public sealed record GetAvailabilityByCityQuery(
    int CityId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int? Adults,
    int? Children,
    int? AvailableRooms,
    int? MinPrice,
    int? MaxPrice,
    int[]? Stars,
    string[]? Accommodations
) : IRequest<GetAvailabilityByCityDto>;