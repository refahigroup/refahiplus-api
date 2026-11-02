using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

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

    private Task<T> GetAsync<T>(string url, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, url, null, null, ct);
    private Task<T> PostAsync<T>(string url, object payload, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, url, payload, null, ct);
    private Task<T> PostAsync<T>(string url, object payload, string idempotencyKey) => SendAsync<T>(HttpMethod.Post, url, payload, idempotencyKey);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? payload, string? idempotencyKey, CancellationToken ct = default)
    {
        var mutation = method != HttpMethod.Get && url.StartsWith("/booking/", StringComparison.OrdinalIgnoreCase);
        using var request = new HttpRequestMessage(method, url);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        }
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            using var response = await _http.SendAsync(request, ct);
            _logger.LogInformation("Hotel provider HTTP operation Provider=SnappTrip Method={Method} Status={Status} ElapsedMs={ElapsedMs}",
                method, (int)response.StatusCode, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (!response.IsSuccessStatusCode) await ThrowApiError(response, url);
            var result = await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new System.Text.Json.JsonException();
            outcome = "success";
            return result;
        }
        catch (Refahi.Modules.Hotels.Application.Contracts.Catalogue.HotelExternalOutcomeUnknownException) { outcome = "unknown"; throw; }
        catch (Exception ex) when (mutation && ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Polly.Timeout.TimeoutRejectedException)
        {
            outcome = "unknown";
            throw new Refahi.Modules.Hotels.Application.Contracts.Catalogue.HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو هنوز مشخص نیست.", ex);
        }
        finally
        {
            Catalogue.HotelProviderTelemetry.Record("SnappTrip", mutation ? "booking-mutation" : "read", outcome,
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private Task ThrowApiError(HttpResponseMessage response, string url)
    {
        _logger.LogWarning("Hotel provider rejected request Provider=SnappTrip Status={Status}", (int)response.StatusCode);
        if (url.StartsWith("/booking/", StringComparison.OrdinalIgnoreCase) && ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout))
            throw new Refahi.Modules.Hotels.Application.Contracts.Catalogue.HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو هنوز مشخص نیست.");
        throw new InvalidOperationException("عملیات هتل توسط تأمین‌کننده پذیرفته نشد.");
    }

    #endregion


    // ============================================================
    // 1) CITY AVAILABILITY
    // ============================================================

    public Task<SnappTripCityAvailabilityResponse> SearchCityAvailabilityAsync(
        SnappTripCityAvailabilityRequest request, CancellationToken ct = default
    )
    {
        return PostAsync<SnappTripCityAvailabilityResponse>("/availability/cities", request, ct);
    }

    // ============================================================
    // 2) HOTEL AVAILABILITY (REAL-TIME)
    // ============================================================

    public Task<IEnumerable<SnappTripRoomAvailability>> GetHotelAvailabilityAsync(
        long[] hotelIds,
        string checkIn,
        string checkOut
    )
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()));
        var url = $"/availability/hotels?id={ids}&checkin={checkIn}&checkout={checkOut}";

        return GetAsync<IEnumerable<SnappTripRoomAvailability>>(url);
    }

    // ============================================================
    // 3) HOTEL AVAILABILITY (REAL-TIME) - Single Hotel
    // ============================================================

    public Task<SnappTripAvailabilityResponse> GetHotelAvailabilityByIdAsync(
        long hotelId,
        string checkIn,
        string checkOut, CancellationToken ct = default
    )
    {
        var url = $"/availability/hotels/{hotelId}?checkin={checkIn}&checkout={checkOut}";
        return GetAsync<SnappTripAvailabilityResponse>(url, ct);
    }

    // ============================================================
    // 4) HOTEL CALENDAR
    // ============================================================

    public Task<SnappTripHotelCalendarResponse> GetHotelCalendarAsync(
        long hotelId,
        string from,
        string to
    )
    {
        var url = $"/availability/hotels/{hotelId}/calendar?from={from}&to={to}";
        return GetAsync<SnappTripHotelCalendarResponse>(url);
    }

    // ============================================================
    // 5) ROOM CALENDAR
    // ============================================================

    public Task<IEnumerable<SnappTripRoomCalendarDay>> GetRoomCalendarAsync(
        long hotelId,
        long roomId,
        string from,
        string to
    )
    {
        var url = $"/availability/hotels/{hotelId}/room/{roomId}/calendar?from={from}&to={to}";
        return GetAsync<IEnumerable<SnappTripRoomCalendarDay>>(url);
    }

    // ============================================================
    // 2) HOTEL AVAILABILITY (REAL-TIME)
    // ============================================================

    public Task<IEnumerable<SnappTripAvailabilityResponse>> GetHotelsAvailabilityAsync(
        long[] hotelIds,
        string checkIn,
        string checkOut
    )
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

    public Task<SnappTripBookingCreateResponse> CreateBookingAsync(
        SnappTripCreateBookingRequest req,
        string idempotencyKey
    )
    {
        return PostAsync<SnappTripBookingCreateResponse>("/booking/create", req, idempotencyKey);
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
            new { } // body خالی
        );
    }

    // ============================================================
    // 11) CITIES LIST
    // ============================================================

    public Task<IEnumerable<SnappTripCityData>> GetCitiesAsync(CancellationToken ct = default)
    {
        return GetAsync<IEnumerable<SnappTripCityData>>("/cities/", ct);
    }

    // ============================================================
    // 12) CITY HOTELS
    // ============================================================

    public Task<IEnumerable<SnappTripHotelBrief>> GetCityHotelsAsync(
        long cityId,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default
    )
    {
        var url = $"/cities/{cityId}/hotels";
        var queryParams = new List<string>();

        if (limit.HasValue)
            queryParams.Add($"limit={limit.Value}");

        if (offset.HasValue)
            queryParams.Add($"offset={offset.Value}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return GetAsync<IEnumerable<SnappTripHotelBrief>>(url, ct);
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

    public Task<IEnumerable<SnappTripHotelDetailsResponse>> GetHotelsDetailsAsync(long[] hotelIds, CancellationToken ct = default)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelDetailsResponse>>(url, ct);
    }

    // ============================================================
    // 15) HOTEL FACILITIES (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelFacilitiesResponse>> GetHotelsFacilitiesAsync(
        long[] hotelIds, CancellationToken ct = default
    )
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/facilities?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelFacilitiesResponse>>(url, ct);
    }

    // ============================================================
    // 16) HOTEL GALLERIES (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelGalleriesResponse>> GetHotelsGalleriesAsync(
        long[] hotelIds, CancellationToken ct = default
    )
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/galleries?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelGalleriesResponse>>(url, ct);
    }

    // ============================================================
    // 17) HOTEL ROOMS (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelRoomsResponse>> GetHotelsRoomsAsync(long[] hotelIds, CancellationToken ct = default)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/rooms?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelRoomsResponse>>(url, ct);
    }

    // ============================================================
    // 18) HOTEL REVIEWS (STATIC)
    // ============================================================

    public Task<IEnumerable<SnappTripHotelReviewsResponse>> GetHotelsReviewsAsync(long[] hotelIds, CancellationToken ct = default)
    {
        var ids = string.Join(',', hotelIds.Select(x => x.ToString()).Take(10)); // Max 10 per API docs
        var url = $"/hotels/reviews?id={ids}";
        return GetAsync<IEnumerable<SnappTripHotelReviewsResponse>>(url, ct);
    }
}
