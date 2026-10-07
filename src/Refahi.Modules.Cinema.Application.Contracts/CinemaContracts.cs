using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Cinema.Application.Contracts;

public sealed record CinemaCity(int Id,string Name,string Province);
public sealed record CinemaShow(string Id, string Title, string Kind, string? Poster, string? Cover,
    string? Summary, string? Description, int? DurationMinutes, string? AgeGroup, string[] Artists);
public sealed record CinemaCatalog(IReadOnlyList<CinemaShow> Items, int Page, int TotalPages);
public sealed record CinemaBanner(string Title, string ImageUrl, string? Url);
public sealed record CinemaRanking(int Rank, string ShowId, string Title, long? AmountMinor);
public sealed record CinemaLanding(IReadOnlyList<CinemaBanner> Banners, IReadOnlyList<CinemaShow> Cinema,
    IReadOnlyList<CinemaShow> Theater, IReadOnlyList<CinemaShow> Art, IReadOnlyList<CinemaRanking> Rankings);
public sealed record CinemaPlace(string Id, string Title, string? Address, string? City);
public sealed record CinemaDisplayDay(string Date, string Weekday, string Label, IReadOnlyList<CinemaPlace> Places);
public sealed record CinemaSession(string Id, string Hall, DateTimeOffset StartsAt, DateTimeOffset? PurchaseEndAt,
    string Status, int AvailableCount, long? PriceMinor, long[] PricesMinor, string? Label);
public sealed record CinemaSeat(string Id, string BlockId, string Row, string Number, double X, double Y,
    long PriceMinor, string Status, bool IsBookable, string? Accessibility);
public sealed record CinemaSeatMap(string ScheduleId, CinemaShow Show, CinemaPlace Place, string Hall,
    DateTimeOffset StartsAt, DateTimeOffset? PurchaseEndAt, IReadOnlyList<CinemaSeat> Seats, string? HallId = null);
public sealed record CinemaSeatStatuses(IReadOnlyDictionary<string, string> Seats);
public sealed record CinemaCustomer(string Mobile, string? Name);
public sealed record CinemaReservation(string Id, string Code, string Status, DateTimeOffset? ExpiresAt,
    long SubtotalMinor, long DiscountMinor, long FeeMinor, long TaxMinor, long TotalMinor,
    string? SamfaCode, long? SamfaId);
public sealed record CinemaOrderView(Guid Id, Guid? OrderId, string? OrderNumber, long Version,
    string PaymentStatus, string IssuanceStatus, string CancellationStatus, DateTimeOffset? PayableUntil,
    long TotalMinor, long SubtotalMinor, long DiscountMinor, long FeeMinor, long TaxMinor,
    CinemaSeatMap Snapshot, IReadOnlyList<string> SeatIds, string? TicketCode, string? SamfaCode,
    bool CanCheckout, bool CanDownload, bool CanCancel, string? Message);
public sealed record CinemaCheckoutResult(Guid CinemaOrderId, Guid OrderId, string OrderNumber, long TotalMinor);

public interface ICinemaProvider
{
    string Key { get; }
    Task<IReadOnlyList<CinemaCity>> CitiesAsync(CancellationToken ct);
    Task<CinemaLanding> LandingAsync(int? city, CancellationToken ct);
    Task<CinemaCatalog> ShowsAsync(string kind, int? city, string? search, int page, CancellationToken ct);
    Task<CinemaShow> ShowAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<CinemaDisplayDay>> PlacesAsync(string show, int? city, string? search, CancellationToken ct);
    Task<IReadOnlyList<CinemaSession>> SessionsAsync(string show, string place, string? date, CancellationToken ct);
    Task<CinemaSeatMap> SeatsAsync(string schedule, CancellationToken ct);
    Task<CinemaSeatStatuses> StatusAsync(string schedule, CancellationToken ct);
    Task<CinemaReservation> ReserveAsync(string schedule, IReadOnlyList<string> seats, CinemaCustomer customer, CancellationToken ct);
    Task<CinemaReservation> GetReservationAsync(string id, CancellationToken ct);
    Task<CinemaReservation> ConfirmAsync(string id, CancellationToken ct);
    Task CancelAsync(string id, CancellationToken ct);
    bool IsReserved(string status);
    bool IsConfirmed(string status);
    bool IsCancelled(string status);
}
public interface ICinemaProviderFactory { ICinemaProvider Get(string key); }
public interface ICinemaMutationLock { Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct); }
public interface ICinemaTicketRenderer { Task<byte[]> RenderAsync(CinemaOrderView ticket, CancellationToken ct); }
public sealed class CinemaOptions
{
    public bool CatalogEnabled { get; set; }
    public bool PurchaseEnabled { get; set; }
    public bool CancellationEnabled { get; set; }
    public string ProviderKey { get; set; } = "iticket";
    public int SafetySeconds { get; set; } = 60;
    public int WorkerSeconds { get; set; } = 15;
}
public sealed class CinemaException(string message, int status = 409) : Exception(message)
{ public int Status { get; } = status; }
public sealed class CinemaProviderAmbiguousException(Exception inner, string? providerOrderId = null) : Exception("نتیجه عملیات تأمین‌کننده مشخص نیست", inner)
{ public string? ProviderOrderId { get; } = providerOrderId; }

public sealed record ReconcileCinemaOrderCommand(Guid Id) : IRequest;
