namespace Refahi.Modules.Cinema.Domain;

public sealed class CinemaOrder
{
    private CinemaOrder() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string ScheduleId { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string SnapshotJson { get; private set; } = "";
    public string SeatIdsJson { get; private set; } = "";
    public string CategoryCode { get; private set; } = "";
    public string? ProviderOrderId { get; private set; }
    public string? TicketCode { get; private set; }
    public string? SamfaCode { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? OrderNumber { get; private set; }
    public string PaymentStatus { get; private set; } = "Unpaid";
    public string IssuanceStatus { get; private set; } = "Reserving";
    public string CancellationStatus { get; private set; } = "None";
    public string? Message { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PayableUntil { get; private set; }
    public long TotalMinor { get; private set; }
    public long SubtotalMinor { get; private set; }
    public long DiscountMinor { get; private set; }
    public long FeeMinor { get; private set; }
    public long TaxMinor { get; private set; }
    public long Version { get; private set; }
    private readonly List<CinemaProviderAttempt> _attempts = [];
    public IReadOnlyCollection<CinemaProviderAttempt> Attempts => _attempts;
    public bool IsPayable(DateTimeOffset now) => IssuanceStatus == "Reserved" && CancellationStatus == "None"
        && PaymentStatus == "Unpaid" && PayableUntil > now && TotalMinor > 0;
    public bool HasTicket => IssuanceStatus == "Issued" && PaymentStatus == "Paid" && CancellationStatus == "None";

    public static CinemaOrder Create(Guid user, string provider, string schedule, string key, string fingerprint,
        string snapshot, string seats, string category)
    {
        if (user == Guid.Empty || string.IsNullOrWhiteSpace(key) || category is not ("cinema" or "theater"))
            throw new InvalidOperationException("اطلاعات سفارش معتبر نیست");
        return new() { Id = Guid.NewGuid(), UserId = user, ProviderKey = provider, ScheduleId = schedule,
            IdempotencyKey = key, Fingerprint = fingerprint, SnapshotJson = snapshot, SeatIdsJson = seats,
            CategoryCode = category, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Version = 1 };
    }
    public void RecordProviderReference(string id)
    {
        if(string.IsNullOrWhiteSpace(id))return;
        if(ProviderOrderId!=null&&ProviderOrderId!=id)throw new InvalidOperationException("شناسه رزرو تغییر کرده است");
        ProviderOrderId=id;Touch();
    }
    public void Reserve(string id, string code, DateTimeOffset? deadline, long subtotal, long discount, long fee, long tax, long total)
    {
        if (string.IsNullOrWhiteSpace(id) || total <= 0 || subtotal < 0 || discount < 0 || fee < 0 || tax < 0
            || checked(subtotal - discount + fee + tax) != total)
            throw new InvalidOperationException("مبلغ رزرو تأمین‌کننده معتبر نیست");
        ProviderOrderId = id; TicketCode = code; PayableUntil = deadline; SubtotalMinor = subtotal;
        DiscountMinor = discount; FeeMinor = fee; TaxMinor = tax; TotalMinor = total;
        IssuanceStatus = "Reserved"; Message = null; Touch();
    }
    public void AttachOrder(Guid id, string number)
    {
        if (OrderId.HasValue && OrderId != id) throw new InvalidOperationException("سفارش مالی دیگری ثبت شده است");
        OrderId = id; OrderNumber = number; Touch();
    }
    public void MarkPaid() { PaymentStatus = "Paid"; Touch(); }
    public void Issue(string code, string? samfa)
    {
        if (PaymentStatus != "Paid" || CancellationStatus != "None" || string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(samfa))
            throw new InvalidOperationException("صدور بدون کد معتبر یا در وضعیت فعلی ممکن نیست");
        TicketCode = code; SamfaCode = samfa; IssuanceStatus = "Issued"; Message = null; Touch();
    }
    public void BeginCancellation() { CancellationStatus = "Pending"; Touch(); }
    public void MarkCancelled() { CancellationStatus = "Cancelled"; IssuanceStatus = "Cancelled"; Touch(); }
    public void RejectCancellation() { CancellationStatus = "None"; Message = "لغو سفارش توسط تأمین‌کننده پذیرفته نشد"; Touch(); }
    public void MarkRefunded() { PaymentStatus = "Refunded"; Touch(); }
    public void MarkFailed(string message) { IssuanceStatus = "Failed"; Message = message; Touch(); }
    public void RequireReview(string message) { IssuanceStatus = "NeedsReview"; Message = message; Touch(); }
    public CinemaProviderAttempt BeginAttempt(string operation)
    {
        var attempt = CinemaProviderAttempt.Create(Id, operation); _attempts.Add(attempt); Touch(); return attempt;
    }
    public void Touch() { UpdatedAt = DateTimeOffset.UtcNow; Version++; }
}
public sealed class CinemaProviderAttempt
{
    private CinemaProviderAttempt() { }
    public Guid Id { get; private set; }
    public Guid CinemaOrderId { get; private set; }
    public string Operation { get; private set; } = "";
    public string Outcome { get; private set; } = "Started";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public static CinemaProviderAttempt Create(Guid order, string operation) => new()
    { Id = Guid.NewGuid(), CinemaOrderId = order, Operation = operation, StartedAt = DateTimeOffset.UtcNow };
    public void Complete() { Outcome = "Completed"; FinishedAt = DateTimeOffset.UtcNow; }
    public void Fail(bool ambiguous) { Outcome = ambiguous ? "Ambiguous" : "Failed"; FinishedAt = DateTimeOffset.UtcNow; }
}
public interface ICinemaOrderRepository
{
    Task<CinemaOrder?> GetAsync(Guid id, CancellationToken ct);
    Task<CinemaOrder?> FindAsync(Guid user, string key, CancellationToken ct);
    Task<IReadOnlyList<Guid>> CandidatesAsync(CancellationToken ct, int skip = 0);
    Task AddAsync(CinemaOrder order, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
