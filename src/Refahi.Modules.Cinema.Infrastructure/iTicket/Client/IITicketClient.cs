#nullable enable

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

/// <summary>
/// iTicket Consumer/Reseller API client generated from the supplied OpenAPI 3.1 contract.
/// Base URL in the contract: https://console.iticket.ir/api/v1
/// Authentication: X-Api-Access-Token.
/// </summary>
public interface IITicketClient
{
    Task<BannerPlacementResourceCollection> ListBannerPlacementsAsyncAsync(ListBannerPlacementsRequest request, CancellationToken cancellationToken = default);
    Task<BannerResourceCollection> ListBannersAsyncAsync(ListBannersRequest request, CancellationToken cancellationToken = default);
    Task<ResellerScheduleResourceCollection> ListResellerSchedulesAsyncAsync(ListResellerSchedulesRequest request, CancellationToken cancellationToken = default);
    Task<PlaceResourceCollection> ListPlacesAsyncAsync(ListPlacesRequest request, CancellationToken cancellationToken = default);
    Task<HallResourceCollection> ListPlaceHallsAsyncAsync(ListPlaceHallsRequest request, CancellationToken cancellationToken = default);
    Task<PlaceDetailResource> ShowPlaceAsyncAsync(ShowPlaceRequest request, CancellationToken cancellationToken = default);
    Task<HallZoneResourceCollection> ListHallZonesAsyncAsync(ListHallZonesRequest request, CancellationToken cancellationToken = default);
    Task<List<ProvinceItem>> ListProvincesAsyncAsync(ListProvincesRequest request, CancellationToken cancellationToken = default);
    Task<ShowResourceCollection> ListShowsAsyncAsync(ListShowsRequest request, CancellationToken cancellationToken = default);
    Task<ShowDetailResource> ShowShowAsyncAsync(ShowShowRequest request, CancellationToken cancellationToken = default);
    Task<ShowArtistResourceCollection> ListShowArtistsAsyncAsync(ListShowArtistsRequest request, CancellationToken cancellationToken = default);
    Task<ShowArtistDetailResource> ShowArtistAsyncAsync(ShowArtistRequest request, CancellationToken cancellationToken = default);
    Task<ShowCategoryResourceCollection> ListShowCategoriesAsyncAsync(ListShowCategoriesRequest request, CancellationToken cancellationToken = default);
    Task<ShowCategoryDetailResource> ShowCategoryAsyncAsync(ShowCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ShowGenreResourceCollection> ListShowGenresAsyncAsync(ListShowGenresRequest request, CancellationToken cancellationToken = default);
    Task<ShowGenreDetailResource> ShowGenreAsyncAsync(ShowGenreRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleResourceCollection> ListScheduleShowsAsyncAsync(ListScheduleShowsRequest request, CancellationToken cancellationToken = default);
    Task<BoxOfficeRankingResourceCollection> BoxOfficeRankingAsyncAsync(BoxOfficeRankingRequest request, CancellationToken cancellationToken = default);
    Task<ShowSchedulesResource> ListShowPlacesAsyncAsync(ListShowPlacesRequest request, CancellationToken cancellationToken = default);
    Task<SessionResourceCollection> ListSessionsAsyncAsync(ListSessionsRequest request, CancellationToken cancellationToken = default);
    Task<SeatMapResource> SeatMapAsyncAsync(SeatMapRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleSeatStatusesResource> SeatStatusAsyncAsync(SeatStatusRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ReserveResellerOrderAsyncAsync(ReserveResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ShowResellerOrderAsyncAsync(ShowResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderResource> ConfirmResellerOrderAsyncAsync(ConfirmResellerOrderRequest request, CancellationToken cancellationToken = default);
    Task<ResellerOrderCanceledResource> CancelResellerOrderAsyncAsync(CancelResellerOrderRequest request, CancellationToken cancellationToken = default);
}
