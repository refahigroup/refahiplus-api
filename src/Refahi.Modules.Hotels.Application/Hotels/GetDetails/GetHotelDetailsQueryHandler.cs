using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;

namespace Refahi.Modules.Hotels.Application.Hotels.GetDetails
{
    public sealed class GetHotelDetailsQueryHandler
        : IRequestHandler<GetHotelDetailsQuery, IEnumerable<HotelDetailsDto>>
    {
        private readonly IHotelProvider _provider;
        private readonly IExternalMediaService? _media;

        public GetHotelDetailsQueryHandler(IHotelProvider provider, IExternalMediaService? media = null)
        {
            _provider = provider; _media = media;
        }

        public async Task<IEnumerable<HotelDetailsDto>> Handle(
            GetHotelDetailsQuery request,
            CancellationToken cancellationToken
        )
        {
            var hotels = (await _provider.GetHotelDetailsAsync(request)).ToArray();
            foreach (var hotel in hotels) hotel.Images = _media is null ? [] : await _media.GetPublicUrlsAsync(hotel.Images, new("Hotels", "SnappTrip"), cancellationToken);
            return hotels;
        }
    }
}
