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

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;

/// <summary>
/// iTicket Consumer/Reseller API client generated from the supplied OpenAPI 3.1 contract.
/// Base URL in the contract: https://console.iticket.ir/api/v1
/// Authentication: X-Api-Access-Token.
/// </summary>
public interface IiTicketClient
{
    Task<System.Text.Json.JsonElement> GetDocumentAsync(string path, CancellationToken ct);
    Task<System.Text.Json.JsonElement> PostDocumentAsync(string path, object? body, CancellationToken ct);
    Task<BannerPlacementResourceCollection> ListBannerPlacementsAsync(ListBannerPlacementsRequest request, CancellationToken cancellationToken = default);
    Task<BannerResourceCollection> ListBannersAsync(ListBannersRequest request, CancellationToken cancellationToken = default);
    Task<ResellerScheduleResourceCollection> ListResellerSchedulesAsync(ListResellerSchedulesRequest request, CancellationToken cancellationToken = default);
    Task<PlaceResourceCollection> ListPlacesAsync(ListPlacesRequest request, CancellationToken cancellationToken = default);
    Task<HallResourceCollection> ListPlaceHallsAsync(ListPlaceHallsRequest request, CancellationToken cancellationToken = default);
    Task<PlaceDetailResource> ShowPlaceAsync(ShowPlaceRequest request, CancellationToken cancellationToken = default);
    Task<HallZoneResourceCollection> ListHallZonesAsync(ListHallZonesRequest request, CancellationToken cancellationToken = default);
    Task<List<ProvinceItem>> ListProvincesAsync(ListProvincesRequest request, CancellationToken cancellationToken = default);
    Task<ShowResourceCollection> ListShowsAsync(ListShowsRequest request, CancellationToken cancellationToken = default);
    Task<ShowDetailResource> ShowShowAsync(ShowShowRequest request, CancellationToken cancellationToken = default);
    Task<ShowArtistResourceCollection> ListShowArtistsAsync(ListShowArtistsRequest request, CancellationToken cancellationToken = default);
    Task<ShowArtistDetailResource> ShowArtistAsync(ShowArtistRequest request, CancellationToken cancellationToken = default);
    Task<ShowCategoryResourceCollection> ListShowCategoriesAsync(ListShowCategoriesRequest request, CancellationToken cancellationToken = default);
    Task<ShowCategoryDetailResource> ShowCategoryAsync(ShowCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ShowGenreResourceCollection> ListShowGenresAsync(ListShowGenresRequest request, CancellationToken cancellationToken = default);
    Task<ShowGenreDetailResource> ShowGenreAsync(ShowGenreRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleResourceCollection> ListScheduleShowsAsync(ListScheduleShowsRequest request, CancellationToken cancellationToken = default);
    Task<BoxOfficeRankingResourceCollection> BoxOfficeRankingAsync(BoxOfficeRankingRequest request, CancellationToken cancellationToken = default);
    Task<ShowSchedulesResource> ListShowPlacesAsync(ListShowPlacesRequest request, CancellationToken cancellationToken = default);
    Task<SessionResourceCollection> ListSessionsAsync(ListSessionsRequest request, CancellationToken cancellationToken = default);
    Task<SeatMapResource> SeatMapAsync(SeatMapRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleSeatStatusesResource> SeatStatusAsync(SeatStatusRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ReserveResellerOrderAsync(ReserveResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ShowResellerOrderAsync(ShowResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ConfirmResellerOrderAsync(ConfirmResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderCanceledResource> CancelResellerOrderAsync(CancelResellerOrderRequest request, CancellationToken cancellationToken = default);
}
