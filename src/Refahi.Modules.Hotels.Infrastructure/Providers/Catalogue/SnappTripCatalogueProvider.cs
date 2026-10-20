using System.Globalization;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;
using Microsoft.Extensions.Caching.Memory;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

public sealed class SnappTripCatalogueProvider(SnappTripHotelProvider provider, SnappTripApiClient api, IMemoryCache cache, Refahi.Modules.Hotels.Domain.Abstraction.Repositories.IHotelCityMappingRepository mappings) : IHotelCatalogueProvider
{
    public string Name => "SnappTrip";
    public HotelProviderCapabilities Capabilities => new(true, true, true, false, true);
    public Task<IReadOnlyList<string>> ResolveCityIdsAsync(int referenceCityId, CancellationToken ct) =>
        mappings.SnappTripCityIdsAsync(referenceCityId, ct);
    public async Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string city, HotelTravel travel, CancellationToken ct)
    {
        var response = await provider.GetAvailabilityByCity(new GetAvailabilityByCityQuery(int.Parse(city, CultureInfo.InvariantCulture),
            travel.CheckIn, travel.CheckOut, travel.Rooms.Sum(x => x.Adults), travel.Rooms.Sum(x => x.ChildrenAges.Count), travel.Rooms.Count,
            null, null, null, null), ct);
        return (response.Items ?? []).Where(x => x.Hotel is not null && x.Room is not null && x.Room.Price > 0).Select(x =>
            new ProviderHotelOffer(new ProviderHotel(x.Hotel!.Id.ToString(), city, x.Hotel.Title, x.Hotel.Address ?? "", x.Hotel.Stars ?? 0,
                x.Hotel.Images ?? [], AccommodationType: x.Hotel.AccommodationType ?? "hotel"), x.Room!.Id.ToString(), x.Room.Title, "", "BedBreakfast", travel.Rooms.Max(p => p.Adults + p.ChildrenAges.Count),
                x.Room.Price, x.Room.Price, 0)).ToArray();
    }
    public async Task<ProviderHotel> DetailsAsync(string id, string city, CancellationToken ct)
    {
        var detail = (await provider.GetHotelDetailsAsync(new GetHotelDetailsQuery(long.Parse(id, CultureInfo.InvariantCulture), null, null), ct)).FirstOrDefault()
            ?? throw new InvalidOperationException("هتل یافت نشد.");
        return new(id, city, detail.Name, detail.Address, detail.Stars, detail.Images.ToArray(), detail.Description, detail.Facilities.ToArray(), detail.Policies,
            CheckInTime: detail.CheckInTime, CheckOutTime: detail.CheckOutTime);
    }
    public async Task<ProviderHotelOffer> ValidateOfferAsync(ProviderHotelOffer offer, HotelTravel travel, CancellationToken ct)
    {
        var key = $"hotels:SnappTrip:rooms:{offer.Hotel.Id}";
        if (!cache.TryGetValue<IReadOnlyList<SnappTripRoomItem>>(key, out var rooms) || rooms is null)
        {
            rooms = (await api.GetHotelsRoomsAsync([long.Parse(offer.Hotel.Id, CultureInfo.InvariantCulture)], ct)).FirstOrDefault()?.rooms
                ?? throw new InvalidOperationException("اطلاعات اتاق هتل در دسترس نیست.");
            cache.Set(key, rooms, TimeSpan.FromDays(1));
        }
        var room = rooms.SingleOrDefault(x => x.id.ToString(CultureInfo.InvariantCulture) == offer.RoomId)
            ?? throw new HotelCatalogueConflictException("اتاق انتخابی دیگر در دسترس نیست.");
        if (travel.Rooms.Any(x => x.Adults > room.adults + room.extra_bed || x.ChildrenAges.Count > room.children
            || x.Adults + x.ChildrenAges.Count > room.adults + room.children + room.extra_bed))
            throw new HotelCatalogueConflictException("ظرفیت اتاق با مسافران مطابقت ندارد.");
        return offer with { BoardType = room.board_type, Capacity = room.adults + room.children, AdultCapacity = room.adults };
    }
    public Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct) =>
        throw new NotSupportedException("رزرو این تأمین‌کننده از مسیر درخواست هتل انجام می‌شود.");
    public Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct) =>
        throw new NotSupportedException("تأیید این تأمین‌کننده از مسیر درخواست هتل انجام می‌شود.");
}
