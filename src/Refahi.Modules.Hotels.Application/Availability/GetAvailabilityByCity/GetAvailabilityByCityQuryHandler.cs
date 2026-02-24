using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

namespace Refahi.Modules.Hotels.Application.Availability.GetAvailabilityByCity;

public sealed class GetAvailabilityByCityQuryHandler : IRequestHandler<GetAvailabilityByCityQuery, GetAvailabilityByCityDto>
{
    private readonly IHotelProvider _provider;

    public GetAvailabilityByCityQuryHandler(IHotelProvider provider)
    {
        _provider = provider;
    }

    public async Task<GetAvailabilityByCityDto> Handle(GetAvailabilityByCityQuery request, CancellationToken cancellationToken)
    {
        return await _provider.GetAvailabilityByCity(request);
    }
}
