namespace Refahi.Modules.Entertainments.Domain;

public enum EntertainmentSessionStatus : short { Reserving = 1, Reserved = 2, Confirmed = 3, Releasing = 4, Released = 5, Unknown = 6, Expired = 7 }

public sealed class EntertainmentCheckoutSession
{
    private EntertainmentCheckoutSession() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string RequestProtected { get; private set; } = "";
    public string? ReservationProtected { get; private set; }
    public string? ReservationReference { get; private set; }
    public EntertainmentSessionStatus Status { get; private set; }
    public Guid? EntertainmentPurchaseId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PayableUntil { get; private set; }
    public uint Version { get; private set; }
    public Guid? ResolutionActorId { get; private set; }
    public string? ResolutionKey { get; private set; }
    public string? ResolutionFingerprint { get; private set; }
    public string? ResolutionEvidenceProtected { get; private set; }
    public string? ResolutionOutcome { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public void ResolveUnpaid(Guid actor, string key, string fingerprint, string evidence, string outcome)
    {
        if (EntertainmentPurchaseId.HasValue || Status is not (EntertainmentSessionStatus.Unknown or EntertainmentSessionStatus.Reserving or EntertainmentSessionStatus.Releasing))
            throw new EntertainmentDomainException("فقط رزرو نامشخص و فاقد خرید قابل تعیین تکلیف است", "RESERVATION_NOT_RECOVERABLE");
        ResolutionActorId = actor; ResolutionKey = key; ResolutionFingerprint = fingerprint;
        ResolutionEvidenceProtected = evidence; ResolutionOutcome = outcome; ResolvedAtUtc = DateTimeOffset.UtcNow;
        Status = EntertainmentSessionStatus.Released;
    }
    public void RedactResolutionEvidence() => ResolutionEvidenceProtected = null;
    public static EntertainmentCheckoutSession Create(Guid user, string provider, string key, string fingerprint, string request)
        => new() { Id = Guid.NewGuid(), UserId = user, ProviderKey = provider, IdempotencyKey = key,
            Fingerprint = fingerprint, RequestProtected = request, Status = EntertainmentSessionStatus.Reserving, CreatedAt = DateTimeOffset.UtcNow };
    public void Reserved(string reference, string reservation, DateTimeOffset until)
    {
        if (Status != EntertainmentSessionStatus.Reserving) throw new EntertainmentDomainException("وضعیت رزرو تغییر کرده است", "SESSION_STATE");
        ReservationReference = reference; ReservationProtected = reservation; PayableUntil = until; Status = EntertainmentSessionStatus.Reserved;
    }
    public void AttachOrder(Guid id) { EntertainmentPurchaseId = id; Status = EntertainmentSessionStatus.Confirmed; }
    public void Releasing()
    {
        if (EntertainmentPurchaseId.HasValue) throw new EntertainmentDomainException("رزرو به سفارش متصل است", "SESSION_ATTACHED");
        Status = EntertainmentSessionStatus.Releasing;
    }
    public void Released() => Status = EntertainmentSessionStatus.Released;
    public void Unknown() => Status = EntertainmentSessionStatus.Unknown;
    public void RedactPrivateData()
    {
        if (Status is not (EntertainmentSessionStatus.Confirmed or EntertainmentSessionStatus.Released or EntertainmentSessionStatus.Expired))
            throw new EntertainmentDomainException("اطلاعات رزرو فعال قابل حذف نیست", "SESSION_RETENTION_NOT_REACHED");
        RequestProtected = string.Empty; ReservationProtected = null;
    }
}

/// <summary>A durable encrypted provider response checkpoint, saved before business mapping.</summary>
public sealed class EntertainmentProviderReceipt
{
    private EntertainmentProviderReceipt() { }
    public string OperationId { get; private set; } = "";
    public string AccountKey { get; private set; } = "";
    public string PayloadProtected { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    public static EntertainmentProviderReceipt Create(string operation, string account, string payload) => new()
    { OperationId = operation, AccountKey = account, PayloadProtected = payload, UpdatedAt = DateTimeOffset.UtcNow };
    public void Redact() { PayloadProtected = string.Empty; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Update(string payload) { PayloadProtected = payload; UpdatedAt = DateTimeOffset.UtcNow; }
}
