using Microsoft.Extensions.Options;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Banner;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Province;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Schedule;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Session;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Show;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowArtist;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowCategory;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowGenre;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Exceptions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;

public sealed class iTicketClient : IiTicketClient
{
    private readonly HttpClient _httpClient;
    private readonly iTicketOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public iTicketClient(HttpClient httpClient, IOptions<iTicketOptions> options, JsonSerializerOptions? jsonOptions = null)
    {
        _options = options.Value ?? 
            throw new ArgumentNullException(nameof(_options));

        _httpClient = httpClient ?? 
            throw new ArgumentNullException(nameof(httpClient));

        _jsonOptions = jsonOptions ?? 
            new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public Task<BannerPlacementResourceCollection> ListBannerPlacementsAsync(
        ListBannerPlacementsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<BannerPlacementResourceCollection>(
            BuildUrl("/marketing/banners/placements", request), cancellationToken);

    public Task<BannerResourceCollection> ListBannersAsync(
        ListBannersRequest request, CancellationToken cancellationToken = default)
        => GetAsync<BannerResourceCollection>(
            BuildUrl("/marketing/banners", request), cancellationToken);

    public Task<ResellerScheduleResourceCollection> ListResellerSchedulesAsync(
        ListResellerSchedulesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ResellerScheduleResourceCollection>(
            BuildUrl("/reseller/schedules", request), cancellationToken);

    public Task<PlaceResourceCollection> ListPlacesAsync(
        ListPlacesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<PlaceResourceCollection>(
            BuildUrl("/places", request), cancellationToken);

    public Task<HallResourceCollection> ListPlaceHallsAsync(
        ListPlaceHallsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<HallResourceCollection>(
            BuildUrl($"/places/{Encode(request.Place)}/halls", request), cancellationToken);

    public Task<PlaceDetailResource> ShowPlaceAsync(
        ShowPlaceRequest request, CancellationToken cancellationToken = default)
        => GetAsync<PlaceDetailResource>(
            $"/places/{Encode(request.Place)}", cancellationToken);

    public Task<HallZoneResourceCollection> ListHallZonesAsync(
        ListHallZonesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<HallZoneResourceCollection>(
            BuildUrl($"/halls/{Encode(request.Hall)}/zones", request), cancellationToken);

    public Task<List<ProvinceItem>> ListProvincesAsync(
        ListProvincesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<List<ProvinceItem>>("/provinces", cancellationToken);

    public Task<ShowResourceCollection> ListShowsAsync(
        ListShowsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowResourceCollection>(
            BuildUrl("/shows", request), cancellationToken);

    public Task<ShowDetailResource> ShowShowAsync(
        ShowShowRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowDetailResource>(
            $"/shows/{Encode(request.Show)}", cancellationToken);

    public Task<ShowArtistResourceCollection> ListShowArtistsAsync(
        ListShowArtistsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowArtistResourceCollection>(
            BuildUrl("/show-artists", request), cancellationToken);

    public Task<ShowArtistDetailResource> ShowArtistAsync(
        ShowArtistRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowArtistDetailResource>(
            $"/show-artists/{Encode(request.ShowArtist)}", cancellationToken);

    public Task<ShowCategoryResourceCollection> ListShowCategoriesAsync(
        ListShowCategoriesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowCategoryResourceCollection>(
            BuildUrl("/show-categories", request), cancellationToken);

    public Task<ShowCategoryDetailResource> ShowCategoryAsync(
        ShowCategoryRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowCategoryDetailResource>(
            $"/show-categories/{Encode(request.ShowCategory)}", cancellationToken);

    public Task<ShowGenreResourceCollection> ListShowGenresAsync(
        ListShowGenresRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowGenreResourceCollection>(
            BuildUrl("/show-genres", request), cancellationToken);

    public Task<ShowGenreDetailResource> ShowGenreAsync(
        ShowGenreRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowGenreDetailResource>(
            $"/show-genres/{Encode(request.ShowGenre)}", cancellationToken);

    public Task<ScheduleResourceCollection> ListScheduleShowsAsync(
        ListScheduleShowsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ScheduleResourceCollection>(
            BuildUrl("/schedules/shows", request), cancellationToken);

    public Task<BoxOfficeRankingResourceCollection> BoxOfficeRankingAsync(
        BoxOfficeRankingRequest request, CancellationToken cancellationToken = default)
        => GetAsync<BoxOfficeRankingResourceCollection>(
            BuildUrl("/schedules/box-office", request), cancellationToken);

    public Task<ShowSchedulesResource> ListShowPlacesAsync(
        ListShowPlacesRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ShowSchedulesResource>(
            BuildUrl($"/schedules/shows/{Encode(request.Show)}/places", request), cancellationToken);

    public Task<SessionResourceCollection> ListSessionsAsync(
        ListSessionsRequest request, CancellationToken cancellationToken = default)
        => GetAsync<SessionResourceCollection>(
            BuildUrl($"/schedules/shows/{Encode(request.Show)}/places/{Encode(request.Place)}/sessions", request), cancellationToken);

    public Task<SeatMapResource> SeatMapAsync(
        SeatMapRequest request, CancellationToken cancellationToken = default)
        => GetAsync<SeatMapResource>(
            $"/schedules/{Encode(request.Schedule)}/seats", cancellationToken);

    public Task<ScheduleSeatStatusesResource> SeatStatusAsync(
        SeatStatusRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ScheduleSeatStatusesResource>(
            $"/schedules/{Encode(request.Schedule)}/seats/status", cancellationToken);

    public Task<ResellerOrderResource> ReserveResellerOrderAsync(
        ReserveResellerOrderRequest request, CancellationToken cancellationToken = default)
        => PostAsync<ResellerOrderResource>(
            "/reseller/orders/reserve", request, cancellationToken);

    public Task<ResellerOrderResource> ShowResellerOrderAsync(
        ShowResellerOrderRequest request, CancellationToken cancellationToken = default)
        => GetAsync<ResellerOrderResource>(
            $"/reseller/orders/{Encode(request.Order)}", cancellationToken);

    public Task<ResellerOrderResource> ConfirmResellerOrderAsync(
        ConfirmResellerOrderRequest request, CancellationToken cancellationToken = default)
        => PostEmptyAsync<ResellerOrderResource>(
            $"/reseller/orders/{Encode(request.Order)}/confirm", cancellationToken);

    public Task<ResellerOrderCanceledResource> CancelResellerOrderAsync(
        CancelResellerOrderRequest request, CancellationToken cancellationToken = default)
        => PostEmptyAsync<ResellerOrderCanceledResource>(
            $"/reseller/orders/{Encode(request.Order)}/cancel", cancellationToken);

    public Task<JsonElement> GetDocumentAsync(string path, CancellationToken ct)
        => GetAsync<JsonElement>(path, ct);

    public Task<JsonElement> PostDocumentAsync(string path, object? body, CancellationToken ct)
        => body is null ? PostEmptyAsync<JsonElement>(path, ct) : PostAsync<JsonElement>(path, body, ct);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body, options: _jsonOptions);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T> PostEmptyAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        return await SendAsync<T>(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("X-Api-Access-Token", _options.AccessToken);
        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ITicketTransportException("The iTicket request timed out.", new TimeoutException());
        }
        catch (HttpRequestException ex)
        {
            throw new ITicketTransportException("The iTicket request failed.", ex);
        }

        using var ownedResponse = response;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            ITicketErrorResponse? error = null;

            try
            {
                error = await JsonSerializer.DeserializeAsync<ITicketErrorResponse>(
                    stream, _jsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                // Keep the HTTP exception useful even when the upstream error body is malformed.
            }

            var message = error?.Message;
            if (string.IsNullOrWhiteSpace(message))
                message = $"iTicket API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).";

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ITicketNotFoundException(response.StatusCode, message, error?.Code, error?.Errors);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
                throw new ITicketValidationException(response.StatusCode, message, error?.Code, error?.Errors);

            throw new ITicketApiException(response.StatusCode, message, error?.Code, error?.Errors);
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            // JSON:API wraps single resources in data; collection DTOs own that property.
            if (typeof(T) != typeof(JsonElement) && root.TryGetProperty("data", out var data)
                && !typeof(T).GetProperties().Any(p => string.Equals(p.Name, "Data", StringComparison.OrdinalIgnoreCase)))
                root = data;
            var result = root.Deserialize<T>(_jsonOptions);
            return result ?? throw new ITicketException(
                $"iTicket returned an empty response for {request.RequestUri}.");
        }
        catch (JsonException ex)
        {
            throw new ITicketException(
                $"Unable to deserialize the iTicket response for {request.RequestUri}.", ex);
        }
    }

    private string BuildUrl<T>(string path, T request)
    {
        var query = new List<string>();

        foreach (var property in typeof(T).GetProperties())
        {
            var attribute = property.GetCustomAttributes(typeof(ITicketQueryNameAttribute), false);
            if (attribute.Length == 0)
                continue;

            var queryName = ((ITicketQueryNameAttribute)attribute[0]).Name;
            var value = property.GetValue(request);
            if (value is null)
                continue;

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is not null)
                        query.Add($"{Encode(queryName)}={Encode(ConvertToString(item))}");
                }
            }
            else
            {
                query.Add($"{Encode(queryName)}={Encode(ConvertToString(value))}");
            }
        }

        return query.Count == 0 ? path : $"{path}?{string.Join("&", query)}";
    }

    private static string ConvertToString(object value) => value switch
    {
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static string Encode(object? value) =>
        Uri.EscapeDataString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
}
