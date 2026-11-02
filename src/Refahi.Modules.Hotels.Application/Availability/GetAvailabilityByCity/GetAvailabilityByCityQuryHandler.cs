using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;

namespace Refahi.Modules.Hotels.Application.Availability.GetAvailabilityByCity;

public sealed class GetAvailabilityByCityQuryHandler
    : IRequestHandler<GetAvailabilityByCityQuery, GetAvailabilityByCityDto>
{
    private readonly IHotelProvider _provider;
        private readonly IExternalMediaService? _media;

    public GetAvailabilityByCityQuryHandler(IHotelProvider provider, IExternalMediaService? media = null)
    {
        _provider = provider; _media = media;
    }

    public async Task<GetAvailabilityByCityDto> Handle(
        GetAvailabilityByCityQuery request,
        CancellationToken cancellationToken
    )
    {
        var result = await _provider.GetAvailabilityByCity(request);
        if (result.Items is not null)
            for (var i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                if (item.Hotel is not null) result.Items[i] = item with { Hotel = item.Hotel with { Images = _media is null ? [] : await _media.GetPublicUrlsAsync(item.Hotel.Images ?? [], new("Hotels", "SnappTrip"), cancellationToken) } };
            }
        return result;
    }
}
