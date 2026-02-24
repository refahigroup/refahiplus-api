using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;

public sealed record GetHotelDetailsQuery(
    long HotelId,
    DateOnly? CheckIn,
    DateOnly? CheckOut
) : IRequest<IEnumerable<HotelDetailsDto>>;