using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Account;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Hotel;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;


namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip
{
    public class SnappTripHotelProvider : IHotelProvider
    {
        private readonly SnappTripApiClient _apiClient;
        private readonly ILogger<SnappTripHotelProvider> _logger;

        public SnappTripHotelProvider(
            SnappTripApiClient apiClient,
            ILogger<SnappTripHotelProvider> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        // ---------------------------------------------------------
        // SEARCH BY CITY  (در حال حاضر تست شده و کار می‌کند)
        // ---------------------------------------------------------
        public async Task<IEnumerable<HotelSearchResultDto>> SearchHotelsAsync(SearchHotelsQuery query)
        {
            var request = new SnappTripCityAvailabilityRequest
            {
                city_id = query.CityId,
                checkin = query.CheckIn.ToString("yyyy-MM-dd"),
                checkout = query.CheckOut.ToString("yyyy-MM-dd"),
                adults = query.Adults ?? 0,
                children = query.Children ?? 0,
                available_rooms = 1,
                min_price = 0,
                max_price = 0,
                stars = new List<int>(),
                accommodations = new List<string>()
            };

            var response = await _apiClient.SearchCityAvailabilityAsync(request);

            // Map از SnappTripCityAvailabilityResponse به HotelSearchResultDto
            return response.items.Select(x => new HotelSearchResultDto
            (
                x.hotel.id,
                x.hotel.title,
                x.city_id,
                x.hotel.stars,
                x.room.price_off > 0 ? x.room.price_off : x.room.price
                //Currency = "IRR",
                //ThumbnailUrl = null // برای thumbnail بعداً می‌توانیم از galleries استفاده کنیم
            ));
        }

        // ---------------------------------------------------------
        // Hotel details (full)
        // ---------------------------------------------------------
        public async Task<IEnumerable<HotelDetailsDto>> GetHotelDetailsAsync(GetHotelDetailsQuery query)
        {
            _logger.LogInformation("SnappTrip: GetHotelDetails - HotelId={HotelId}", query.HotelId);

            // Get real-time availability and static details/galleries/rooms
            var checkIn = query.CheckIn?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            var checkOut = query.CheckOut?.ToString("yyyy-MM-dd") ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd");

            var availability = await _apiClient.GetHotelAvailabilityByIdAsync(query.HotelId, checkIn, checkOut);
            var detailsList = await _apiClient.GetHotelsDetailsAsync(new[] { query.HotelId });
            var galleriesList = await _apiClient.GetHotelsGalleriesAsync(new[] { query.HotelId });
            var roomsStatic = await _apiClient.GetHotelsRoomsAsync(new[] { query.HotelId });
            var facilitiesStatic = await _apiClient.GetHotelsFacilitiesAsync(new[] { query.HotelId });

            var detail = detailsList?.FirstOrDefault()?.hotel;
            if (detail == null)
                return Enumerable.Empty<HotelDetailsDto>();

            // Map images
            var images = galleriesList?.FirstOrDefault()?.gallery?.Select(g => g.url).Where(u => !string.IsNullOrEmpty(u)).ToList() ?? new List<string>();

            // Map hotel facilities
            var facilities = detail.facilities?.Select(f => f.title).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();

            // Build room lookup from static rooms
            var staticRooms = roomsStatic?.FirstOrDefault()?.rooms?.ToDictionary(r => (long)r.id) ?? new Dictionary<long, Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract.SnappTripRoomItem>();

            // Build pricing lookup from availability (pricing in availability.availability items)
            var pricingLookup = new Dictionary<long, Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract.SnappTripRoomPricing>();
            if (availability?.availability != null)
            {
                foreach (var r in availability.availability)
                {
                    try
                    {
                        pricingLookup[r.room.id] = r.pricing;
                    }
                    catch
                    {
                        // ignore malformed entries
                    }
                }
            }

            var rooms = new List<HotelRoomDto>();
            foreach (var kv in staticRooms)
            {
                var roomId = kv.Key;
                var roomItem = kv.Value;

                var price = pricingLookup.TryGetValue(roomId, out var pr) ? pr.price : 0;

                var roomDto = new HotelRoomDto
                {
                    RoomId = roomId,
                    RoomName = roomItem.title,
                    Capacity = roomItem.adults + roomItem.children,
                    CustomerPrice = price,
                    BoardType = roomItem.board_type ?? string.Empty,
                    Description = roomItem.description ?? string.Empty,
                    Adults = roomItem.adults,
                    Children = roomItem.children,
                    Facilities = roomItem.facilities_tags?
                        .SelectMany(t => t.facilities.Select(f => f.title))
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()
                        .ToList() ?? new List<string>()
                };

                rooms.Add(roomDto);
            }

            var hotelDto = new HotelDetailsDto
            {
                HotelId = detail.id,
                Name = detail.title,
                CityName = detail.city?.title ?? string.Empty,
                Description = detail.description ?? string.Empty,
                Address = detail.address ?? string.Empty,
                Stars = detail.stars,
                Images = images,
                Facilities = facilities,
                Rooms = rooms
            };

            return new[] { hotelDto };
        }

        // ---------------------------------------------------------
        // BOOKING LIFECYCLE
        // ---------------------------------------------------------

        public async Task<BookingCreateResultDto> CreateBookingAsync(BookingDraftDto request)
        {
            _logger.LogInformation("SnappTrip CreateBooking: HotelId={HotelId}, RoomId={RoomId}", 
                request.HotelId, request.RoomId);

            // Map BookingDraftDto به SnappTripCreateBookingRequest
            var snappTripRequest = new SnappTripCreateBookingRequest
            {
                hotel_id = (int)request.HotelId,
                checkin = request.CheckIn.ToString("yyyy-MM-dd"),
                checkout = request.CheckOut.ToString("yyyy-MM-dd"),
                email = string.Empty,   // باید از جایی دیگر پر شود (User info)
                phone = string.Empty,   // باید از جایی دیگر پر شود (User info)
                note = null,
                rooms = MapRooms(request)
            };

            // Call API
            var response = await _apiClient.CreateBookingAsync(snappTripRequest);

            // Map response to DTO
            return SnappTripMapper.MapCreateBooking(response);
        }

        public async Task LockBookingAsync(string bookingCode)
        {
            _logger.LogInformation("SnappTrip LockBooking: BookingCode={BookingCode}", bookingCode);
            await _apiClient.LockBookingAsync(bookingCode);
        }

        public async Task ConfirmBookingAsync(string bookingCode)
        {
            _logger.LogInformation("SnappTrip ConfirmBooking: BookingCode={BookingCode}", bookingCode);
            await _apiClient.ConfirmBookingAsync(bookingCode);
        }

        public async Task<BookingStatusDto> GetBookingStatusAsync(string bookingCode)
        {
            _logger.LogInformation("SnappTrip GetBookingStatus: BookingCode={BookingCode}", bookingCode);
            
            var response = await _apiClient.GetBookingStatusAsync(bookingCode);
            return SnappTripMapper.MapStatus(response);
        }

        // ---------------------------------------------------------
        // HELPER METHODS
        // ---------------------------------------------------------

        private List<SnappTripBookRoom> MapRooms(BookingDraftDto request)
        {
            var room = new SnappTripBookRoom
            {
                room_id = (int)request.RoomId,
                children = 0,
                infants = 0,
                extra_beds = 0,
                guests = request.Guests
                    .Select(g => MapGuest(g))
                    .ToList()
            };

            return new List<SnappTripBookRoom> { room };
        }

        private SnappTripGuest MapGuest(GuestDto guest)
        {
            var names = guest.FullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            var firstName = names.Length > 0 ? names[0] : "Unknown";
            var lastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "User";

            return new SnappTripGuest
            {
                first_name = firstName,
                last_name = lastName,
                foreigner = false
            };
        }

        public async Task<IEnumerable<GetCitiesResponse>> GetAllCities(string? name)
        {
            _logger.LogInformation("SnappTrip GetAllCities: Name={CityName}", name ?? "all");

            var cities = await _apiClient.GetCitiesAsync();
            var mapped = cities.Select(c => new GetCitiesResponse(
                c.id,
                c.title_fa,
                c.title_en,
                c.state?.id ?? 0,
                c.state?.title ?? string.Empty
            ));

            if (!string.IsNullOrEmpty(name))
            {
                var lowered = name.ToLowerInvariant();
                mapped = mapped.Where(x => x.Name.ToLowerInvariant().Contains(lowered) || x.NameEn.ToLowerInvariant().Contains(lowered));
            }

            return mapped;
        }

        public async Task<GetAvailabilityByCityDto> GetAvailabilityByCity(GetAvailabilityByCityQuery query)
        {
            _logger.LogInformation("SnappTrip GetAvailabilityByCity: CityId={CityId}, CheckIn={CheckIn}, CheckOut={CheckOut}",
                query.CityId, query.CheckIn, query.CheckOut);

            // 1. ایجاد request برای SnappTrip API
            var request = new SnappTripCityAvailabilityRequest
            {
                city_id = query.CityId,
                checkin = query.CheckIn.ToString("yyyy-MM-dd"),
                checkout = query.CheckOut.ToString("yyyy-MM-dd"),
                adults = query.Adults ?? 0,
                children = query.Children ?? 0,
                available_rooms = query.AvailableRooms ?? 1,
                min_price = query.MinPrice ?? 0,
                max_price = query.MaxPrice ?? 0,
                stars = query.Stars?.ToList() ?? new List<int>(),
                accommodations = query.Accommodations?.ToList() ?? new List<string>()
            };

            // 2. صدا زدن API
            var response = await _apiClient.SearchCityAvailabilityAsync(request);

            // 3. Mapping response به Application DTO
            var availabilityItems = response.items
                .Select(item => new AvailabilityByCitiesItem(
                    CityId: item.city_id,
                    Hotel: item.hotel != null ? new AvailabilityByCitiesHotel(
                        Id: item.hotel.id,
                        Title: item.hotel.title,
                        AccommodationType: item.hotel.accommodation_type,
                        AccommodationTitle: item.hotel.accommodation_title,
                        Address: item.hotel.address,
                        Stars: item.hotel.stars
                    ) : null,
                    Room: item.room != null ? new AvailabilityByCitiesRoom(
                        Id: item.room.id,
                        Title: item.room.title,
                        Price: (int)item.room.price,
                        PriceOff: (int?)item.room.price_off,
                        DiscountPercent: item.room.discount_percent,
                        ChildPrice: (int?)item.room.child_price,
                        ExtraBedPrice: (int?)item.room.extra_bed_price,
                        Children: item.room.children
                    ) : null
                ))
                .ToList();

            // 4. تبدیل filter اطلاعات
            var filterDto = new AvailabilityByCitiesFilter(
                MinPrice: response.filter?.min_price,
                MaxPrice: response.filter?.max_price,
                Adults: response.filter?.adults,
                Children: response.filter?.children,
                AvailableRooms: response.filter?.available_rooms,
                Stars: response.filter?.stars,
                Accommodations: response.filter?.accommodations
            );

            // 5. بازگشت نتیجه
            return new GetAvailabilityByCityDto(filterDto, availabilityItems);
        }

        public async Task<AvailabilityCalendarDto> GetHotelAvailabilityCalendarAsync(long hotelId, DateOnly from, DateOnly to)
        {
            _logger.LogInformation("SnappTrip GetHotelAvailabilityCalendar: HotelId={HotelId}, From={From}, To={To}", hotelId, from, to);

            var cal = await _apiClient.GetHotelCalendarAsync(hotelId, from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
            var name = (await _apiClient.GetHotelsDetailsAsync(new[] { hotelId }))?.FirstOrDefault()?.hotel?.title ?? string.Empty;

            var result = new AvailabilityCalendarDto
            {
                HotelId = hotelId,
                HotelName = name,
                FromDate = from,
                ToDate = to,
                RoomCalendars = new Dictionary<long, List<DailyAvailabilityDto>>()
            };

            if (cal?.rooms == null)
                return result;

            foreach (var room in cal.rooms)
            {
                var list = new List<DailyAvailabilityDto>();
                foreach (var kv in room.daily)
                {
                    // Try parse using invariant culture and explicit yyyy-MM-dd format
                    if (!DateOnly.TryParse(kv.Key, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                    {
                        if (!DateOnly.TryParseExact(kv.Key, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                            continue;
                    }

                    var d = kv.Value;
                    var item = new DailyAvailabilityDto
                    {
                        Date = dt,
                        IsAvailable = d.availability > 0,
                        PricePerNight = d.price,
                        RemainingRooms = d.availability,
                        UnavailabilityReason = d.availability > 0 ? null : "ناموجود"
                    };
                    list.Add(item);
                }

                result.RoomCalendars[room.id] = list.OrderBy(x => x.Date).ToList();
            }

            return result;
        }

        public async Task<HotelReviewsDto> GetHotelReviewsAsync(long hotelId, int page = 1, int pageSize = 10)
        {
            _logger.LogInformation("SnappTrip GetHotelReviews: HotelId={HotelId}, Page={Page}, PageSize={PageSize}", hotelId, page, pageSize);

            var resp = await _apiClient.GetHotelsReviewsAsync(new[] { hotelId });
            var reviews = resp?.FirstOrDefault()?.reviews ?? new List<Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract.SnappTripHotelReview>();

            var total = reviews.Count;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);

            // paging in-memory (API does not provide pagination here)
            var pageItems = reviews.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var mappedReviews = pageItems.Select(r => new ReviewDto
            {
                GuestName = r.fullname,
                ReviewDate = ConvertUnixTimeToDateTime(r.registered_date),
                Comment = r.comment ?? string.Empty,
                Rating = (decimal)r.rate_overall,
                StayDate = ConvertUnixTimeToDateOnly(r.registered_date),
                NightsStayed = 1,
                DetailedRatings = new DetailedRatingsDto
                {
                    Cleanliness = (decimal?)r.rate_clean,
                    Comfort = (decimal?)r.rate_sleep_quality,
                    Service = (decimal?)r.rate_staff,
                    ValueForMoney = (decimal?)r.rate_value_for_money,
                    Location = (decimal?)r.rate_location
                }
            }).ToList();

            var overall = reviews.Any() ? (decimal)reviews.Average(x => x.rate_overall) : 0m;

            return new HotelReviewsDto
            {
                HotelId = hotelId,
                HotelName = string.Empty,
                OverallRating = Math.Round(overall, 2),
                TotalReviews = total,
                CurrentPage = page,
                TotalPages = Math.Max(1, totalPages),
                Reviews = mappedReviews,
                RatingSummary = new RatingSummaryDto
                {
                    Cleanliness = mappedReviews.Average(r => r.DetailedRatings?.Cleanliness ?? 0),
                    Comfort = mappedReviews.Average(r => r.DetailedRatings?.Comfort ?? 0),
                    Service = mappedReviews.Average(r => r.DetailedRatings?.Service ?? 0),
                    Value = mappedReviews.Average(r => r.DetailedRatings?.ValueForMoney ?? 0),
                    Location = mappedReviews.Average(r => r.DetailedRatings?.Location ?? 0)
                }
            };
        }

        public async Task<AccountBalanceDto> GetAccountBalanceAsync()
        {
            _logger.LogInformation("SnappTrip GetAccountBalance");

            var resp = await _apiClient.GetBalanceAsync();
            var balance = resp?.balance ?? 0L;

            return new AccountBalanceDto
            {
                AvailableBalance = balance,
                LockedBalance = 0,
                LastUpdated = DateTime.UtcNow,
                Currency = "IRR"
            };
        }

        // Converts unix timestamp (seconds or milliseconds) to DateTime (UTC)
        private static DateTime ConvertUnixTimeToDateTime(long unix)
        {
            try
            {
                if (unix > 9999999999L) // milliseconds
                    return DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime;
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static DateOnly ConvertUnixTimeToDateOnly(long unix)
        {
            var dt = ConvertUnixTimeToDateTime(unix);
            return DateOnly.FromDateTime(dt);
        }
    }
}
