namespace Refahi.Modules.Pools.Application.Contracts;

public interface IPoolOrganizationProvider : IPoolProvider
{
    Task RegisterUserAsync(CancellationToken ct);
    Task<IReadOnlyList<PoolArea>> AreasAsync(string cityId, CancellationToken ct);
    Task<PoolProviderDetails> DetailsAsync(string poolId, CancellationToken ct);
    Task<PoolSessionAvailability> SessionInfoAsync(string poolId, string rowId, CancellationToken ct);
    Task<PoolExternalReservation> ReserveAsync(PoolSelection selection, CancellationToken ct);
    Task<PoolExternalReservation> ReservationInfoAsync(string reservationId, CancellationToken ct);
    Task<PoolReservationHistory> ReservationHistoryAsync(int page, int limit, CancellationToken ct);
}
public sealed record PoolArea(string Id, string CityId, string Name);
public sealed record PoolProviderDetails(string Id, string DescriptionHtml, string TermsHtml, string Address, string? Telephone,
    IReadOnlyList<string> Images, IReadOnlyList<string> Attributes);
public sealed record PoolSessionAvailability(string Id, bool Available, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc);
public sealed record PoolExternalReservation(string Id, string Voucher, string PoolName, string SessionName, int AdultCount,
    int ChildCount, decimal UserPaid, string Date, string Time, int Enter);
public sealed record PoolReservationHistory(int Page, int Limit, IReadOnlyList<PoolExternalReservation> Items);
