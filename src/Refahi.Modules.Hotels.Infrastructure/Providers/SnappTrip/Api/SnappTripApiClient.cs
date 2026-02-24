using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;
using System.Net.Http.Json;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;

/*
    ** Availability
        1) /availability/cities [✓]
        2) /availability/hotels [✓]
        3) /availability/hotels/{id} [✓]
        4) /availability/hotels/{id}/calendar [✓]
        5) /availability/hotels/{id}/room/{room_id}/calendar [✓]

    ** Balance
        6) /balance [✓]
    
    ** Booking
        7) /booking/create [✓]
        8) /booking/{code} [✓]
        9) /booking/{code}/confirm [✓]
        10) /booking/{code}/lock [✓]

    ** Cities
        11) /cities [✓]
        12) /cities/{id}/hotels [✓]

    ** Health
        13) /health [✓]

    ** Hotels
        14) /hotels [✓]
        15) /hotels/facilities [✓]
        16) /hotels/galleries [✓]
        17) /hotels/rooms [✓]
        18) /hotels/reviews [✓]

*/

public class SnappTripApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SnappTripApiClient> _logger;

    public SnappTripApiClient(HttpClient http, ILogger<SnappTripApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }


    #region INTERNAL HELPERS

    private async Task<T> GetAsync<T>(string url)
    {
        _logger.LogInformation("SnappTrip GET {Url}", url);

        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            await ThrowApiError(response, url);

        string json = await response.Content.ReadAsStringAsync();

        _logger.LogInformation(json);

        var result = await response.Content.ReadFromJsonAsync<T>();
        if (result == null)
            throw new Exception($"SnappTrip GET {url} returned NULL");

        return result;
    }

    private async Task<T> PostAsync<T>(string url, object payload)
    {
        _logger.LogInformation("SnappTrip POST {Url}", url);

        var response = await _http.PostAsJsonAsync(url, payload);

        if (!response.IsSuccessStatusCode)
            await ThrowApiError(response, url);

        string json = await response.Content.ReadAsStringAsync();

        var result = await response.Content.ReadFromJsonAsync<T>();
        if (result == null)
            throw new Exception($"SnappTrip POST {url} returned NULL");

        return result;
    }

    private async Task ThrowApiError(HttpResponseMessage response, string url)
    {
        var err = await response.Content.ReadFromJsonAsync<SnappTripApiError>();
        var message = $"SnappTrip Error calling {url}. " +
                      $"Status={(int)response.StatusCode}, " +
                      $"Code={err?.code}, " +
                      $"Message={err?.message}, Trace={err?.trace_id}";

        _logger.LogError(message);
        throw new Exception(message);
    }

    #endregion


    // ============================================================
    // 1) CITY AVAILABILITY
    // ============================================================

    public Task<SnappTripCityAvailabilityResponse> SearchCityAvailabilityAsync(SnappTripCityAvailabilityRequest request)
    {
        return PostAsync<SnappTripCityAvailabilityResponse>("/availability/cities", request);
    }

    // ============================================================
    // 2) HOTEL AVAILABILITY (REAL-TIME)
    // ============================================================

    public Task<IEnumerable<SnappTripRoomAvailability>> GetHotelAvailabilityAsync(long[] hotelIds, string checkIn, string checkOut)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()));
        var url = $"/availability/hotels?id={ids}&checkin={checkIn}&checkout={checkOut}";

        return GetAsync<IEnumerable<SnappTripRoomAvailability>>(url);
    }


    // ============================================================
    // 3) HOTEL AVAILABILITY (REAL-TIME) - Single Hotel
    // ============================================================

    public Task<SnappTripAvailabilityResponse> GetHotelAvailabilityByIdAsync(long hotelId, string checkIn, string checkOut)
    {
        var url = $"/availability/hotels/{hotelId}?checkin={checkIn}&checkout={checkOut}";
        return GetAsync<SnappTripAvailabilityResponse>(url);
    }

    // ============================================================
    // 4) HOTEL CALENDAR
    // ============================================================

    public Task<SnappTripHotelCalendarResponse> GetHotelCalendarAsync(long hotelId, string from, string to)
    {
        var url = $"/availability/hotels/{hotelId}/calendar?from={from}&to={to}";
        return GetAsync<SnappTripHotelCalendarResponse>(url);
    }

    // ============================================================
    // 5) ROOM CALENDAR
    // ============================================================

    public Task<IEnumerable<SnappTripRoomCalendarDay>> GetRoomCalendarAsync(long hotelId, long roomId, string from, string to)
    {
        var url = $"/availability/hotels/{hotelId}/room/{roomId}/calendar?from={from}&to={to}";
        return GetAsync<IEnumerable<SnappTripRoomCalendarDay>>(url);
    }

    // ============================================================
    // 2) HOTEL AVAILABILITY (REAL-TIME)
    // ============================================================

    public Task<IEnumerable<SnappTripAvailabilityResponse>> GetHotelsAvailabilityAsync(long[] hotelIds, string checkIn, string checkOut)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()));
        var url = $"/availability/hotels?id={ids}&checkin={checkIn}&checkout={checkOut}";

        return GetAsync<IEnumerable<SnappTripAvailabilityResponse>>(url);
    }

    // ============================================================
    // 6) BALANCE
    // ============================================================

    public Task<SnappTripBalanceResponse> GetBalanceAsync()
    {
        return GetAsync<SnappTripBalanceResponse>("/balance/");
    }

    // ============================================================
    // 7) CREATE BOOKING
    // ============================================================

    public Task<SnappTripBookingCreateResponse> CreateBookingAsync(SnappTripCreateBookingRequest req)
    {
        return PostAsync<SnappTripBookingCreateResponse>("/booking/create", req);
    }

    // ============================================================
    // 8) BOOKING STATUS
    // ============================================================

    public Task<SnappTripBookingStatusResponse> GetBookingStatusAsync(string reservationCode)
    {
        return GetAsync<SnappTripBookingStatusResponse>($"/booking/{reservationCode}");
    }

    // ============================================================
    // 9) BOOKING LOCK
    // ============================================================

    public async Task LockBookingAsync(string reservationCode)
    {
        _logger.LogInformation("SnappTrip POST /booking/{code}/lock", reservationCode);

        var res = await _http.PostAsync($"/booking/{reservationCode}/lock", null);

        if (!res.IsSuccessStatusCode)
            await ThrowApiError(res, $"/booking/{reservationCode}/lock");
    }

    // ============================================================
    // 10) BOOKING CONFIRM
    // ============================================================

    public Task<SnappTripBookingStatusResponse> ConfirmBookingAsync(string reservationCode)
    {
        return PostAsync<SnappTripBookingStatusResponse>(
            $"/booking/{reservationCode}/confirm",
            new { }  // body خالی
        );
    }




    // ============================================================
    // 11) CITIES LIST
    // ============================================================

    public Task<IEnumerable<SnappTripCityData>> GetCitiesAsync()
    {
        return GetAsync<IEnumerable<SnappTripCityData>>("/cities/");
    }

    // ============================================================
    // 12) CITY HOTELS
    // ============================================================

    public Task<IEnumerable<SnappTripHotelBrief>> GetCityHotelsAsync(long cityId, int? limit = null, int? offset = null)
    {
        var url = $"/cities/{cityId}/hotels";
        var queryParams = new List<string>();

        if (limit.HasValue)
            queryParams.Add($"limit={limit.Value}");

        if (offset.HasValue)
            queryParams.Add($"offset={offset.Value}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return GetAsync<IEnumerable<SnappTripHotelBrief>>(url);
    }

    // ============================================================
    // 13) HEALTH CHECK
    // ============================================================

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            _logger.LogInformation("SnappTrip GET /health");
            var response = await _http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SnappTrip health check failed");
            return false;
        }
    }

    // ============================================================
    // 14) HOTEL DETAILS (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelDetailsResponse>> GetHotelsDetailsAsync(long[] hotelIds)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelDetailsResponse>>(url);
    }

    // ============================================================
    // 15) HOTEL FACILITIES (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelFacilitiesResponse>> GetHotelsFacilitiesAsync(long[] hotelIds)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/facilities?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelFacilitiesResponse>>(url);
    }

    // ============================================================
    // 16) HOTEL GALLERIES (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelGalleriesResponse>> GetHotelsGalleriesAsync(long[] hotelIds)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/galleries?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelGalleriesResponse>>(url);
    }

    // ============================================================
    // 17) HOTEL ROOMS (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelRoomsResponse>> GetHotelsRoomsAsync(long[] hotelIds)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/rooms?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelRoomsResponse>>(url);
    }

    // ============================================================
    // 18) HOTEL REVIEWS (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelReviewsResponse>> GetHotelsReviewsAsync(long[] hotelIds)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/reviews?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelReviewsResponse>>(url);
    }



}
