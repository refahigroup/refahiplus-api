using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;

public sealed record SearchHotelsQuery(
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
) : IRequest<IEnumerable<HotelSearchResultDto>>;
