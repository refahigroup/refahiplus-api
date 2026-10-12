namespace Refahi.Modules.Pools.Domain;

public sealed class PoolBooking
{
    private PoolBooking() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string SnapshotJson { get; private set; } = "";
    public string? ProviderReference { get; private set; }
    public bool IsMock { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? OrderNumber { get; private set; }
    public bool OrderCancellationCompleted { get; private set; }
    public long TotalMinor { get; private set; }
    public DateTimeOffset? PayableUntil { get; private set; }
    public string PaymentStatus { get; private set; } = "Unpaid";
    public string ReservationStatus { get; private set; } = "Reserving";
    public string IssuanceStatus { get; private set; } = "None";
    public string CancellationStatus { get; private set; } = "None";
    public string? TicketCode { get; private set; }
    public string? Message { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<PoolProviderAttempt> _attempts = [];
    public IReadOnlyCollection<PoolProviderAttempt> Attempts => _attempts;
    public bool IsPayable(DateTimeOffset now) => ReservationStatus == "Reserved" && PaymentStatus == "Unpaid"
        && CancellationStatus == "None" && PayableUntil > now && TotalMinor > 0;
    public bool HasTicket => PaymentStatus == "Paid" && IssuanceStatus == "Issued" && CancellationStatus == "None";
    public static PoolBooking Create(Guid user, string provider, string key, string fingerprint, string snapshot, bool isMock)
    {
        if (user == Guid.Empty || string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new InvalidOperationException("اطلاعات درخواست خرید معتبر نیست");
        return new() { Id = Guid.NewGuid(), UserId = user, ProviderKey = provider, IdempotencyKey = key,
            Fingerprint = fingerprint, SnapshotJson = snapshot, IsMock = isMock, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    }
    public void Reserve(string reference, long total, DateTimeOffset deadline)
    {
        if (ReservationStatus != "Reserving" || string.IsNullOrWhiteSpace(reference) || total <= 0 || deadline <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("رزرو معتبر نیست");
        ProviderReference = reference; TotalMinor = total; PayableUntil = deadline; ReservationStatus = "Reserved"; Message = null; Touch();
    }
    public void AttachOrder(Guid id, string number)
    {
        if (id == Guid.Empty || OrderId.HasValue && OrderId != id) throw new InvalidOperationException("مرجع سفارش مالی معتبر نیست");
        OrderId = id; OrderNumber = number; Touch();
    }
    public void MarkPaid() { if (PaymentStatus == "Refunded") throw new InvalidOperationException("سفارش بازگشت داده شده است"); PaymentStatus = "Paid"; Touch(); }
    public void Issue(string code)
    {
        if (PaymentStatus != "Paid" || CancellationStatus != "None" || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("صدور بلیط در وضعیت فعلی ممکن نیست");
        IssuanceStatus = "Issued"; TicketCode = code; Message = null; Touch();
    }
    public void FailReservation() { ReservationStatus = "Failed"; Message = "رزرو انجام نشد"; Touch(); }
    public void FailIssuance() { IssuanceStatus = "Failed"; Message = "صدور انجام نشد؛ بازگشت وجه در حال پیگیری است"; Touch(); }
    public void Review(string message) { Message = message; Touch(); }
    public void BeginCancellation() { CancellationStatus = "Pending"; Touch(); }
    public void Cancel() { CancellationStatus = "Cancelled"; ReservationStatus = "Cancelled"; Touch(); }
    public void MarkRefunded() { PaymentStatus = "Refunded"; Touch(); }
    public void MarkOrderCancellationCompleted()
    {
        if (!OrderId.HasValue || CancellationStatus != "Cancelled") throw new InvalidOperationException("لغو سفارش هنوز آماده نیست");
        if (!OrderCancellationCompleted) { OrderCancellationCompleted = true; Touch(); }
    }
    public PoolProviderAttempt BeginAttempt(string operation)
    {
        var attempt = PoolProviderAttempt.Create(Id, operation); _attempts.Add(attempt); Touch(); return attempt;
    }
    private void Touch() { Version++; UpdatedAt = DateTimeOffset.UtcNow; }
}
public sealed class PoolProviderAttempt
{
    private PoolProviderAttempt() { }
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string Operation { get; private set; } = "";
    public string Outcome { get; private set; } = "Started";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public static PoolProviderAttempt Create(Guid id, string operation) => new() { Id = Guid.NewGuid(), BookingId = id, Operation = operation, StartedAt = DateTimeOffset.UtcNow };
    public void Complete() { Outcome = "Completed"; FinishedAt = DateTimeOffset.UtcNow; }
    public void Fail(bool ambiguous) { Outcome = ambiguous ? "Ambiguous" : "Failed"; FinishedAt = DateTimeOffset.UtcNow; }
}
public interface IPoolBookingRepository
{
    Task<PoolBooking?> GetAsync(Guid id, CancellationToken ct);
    Task<PoolBooking?> FindAsync(Guid user, string key, CancellationToken ct);
    Task<IReadOnlyList<Guid>> CandidatesAsync(DateTimeOffset? afterCreated, Guid? afterId, CancellationToken ct);
    Task AddAsync(PoolBooking booking, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
