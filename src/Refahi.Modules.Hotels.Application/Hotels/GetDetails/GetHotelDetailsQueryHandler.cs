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
        private readonly IHotelLocalContentReader? _local;
        private readonly Features.Catalogue.HotelCatalogueService? _catalogue;

        public GetHotelDetailsQueryHandler(IHotelProvider provider, IExternalMediaService? media = null, IHotelLocalContentReader? local = null,
            Features.Catalogue.HotelCatalogueService? catalogue = null)
        {
            _provider = provider; _media = media; _local = local; _catalogue = catalogue;
        }

        public async Task<IEnumerable<HotelDetailsDto>> Handle(
            GetHotelDetailsQuery request,
            CancellationToken cancellationToken
        )
        {
            if (_local is not null)
            {
                var key = await _local.ResolveLegacyKeyAsync(request.HotelId, cancellationToken);
                if (key.HasValue && await _local.UsesLocalAsync(key.Value, cancellationToken))
                {
                    var profile = await _local.ProfileAsync(key.Value, cancellationToken);
                    if (profile is null) return [];
                    var offers = request.CheckIn.HasValue && request.CheckOut.HasValue && _catalogue is not null
                        ? (await _catalogue.AvailabilityAsync(key.Value, new(request.CheckIn.Value, request.CheckOut.Value, [new(2, [])]), cancellationToken)).Offers : [];
                    return [new HotelDetailsDto { HotelId = request.HotelId, Name = profile.Title, CityName = profile.CityName,
                        Description = profile.Description, Address = profile.Address, Stars = profile.Stars, Images = profile.Images,
                        Facilities = profile.Facilities, Policies = profile.Policies, CheckInTime = profile.Content?.Policies.CheckInTime,
                        CheckOutTime = profile.Content?.Policies.CheckOutTime,
                        Rooms = offers.Select(x => new HotelRoomDto { RoomName = x.RoomName, Capacity = x.Capacity, CustomerPrice = x.PriceMinor, BoardType = x.BoardType, Facilities = [] }).ToArray() }];
                }
            }
            var hotels = (await _provider.GetHotelDetailsAsync(request)).ToArray();
            foreach (var hotel in hotels) hotel.Images = _media is null ? [] : await _media.GetPublicUrlsAsync(hotel.Images, new("Hotels", "SnappTrip"), cancellationToken);
            return hotels;
        }
    }
}
