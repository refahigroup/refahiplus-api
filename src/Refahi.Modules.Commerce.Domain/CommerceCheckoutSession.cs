namespace Refahi.Modules.Commerce.Domain;

public enum CommerceSessionStatus : short { Reserving = 1, Reserved = 2, Confirmed = 3, Releasing = 4, Released = 5, Unknown = 6, Expired = 7 }

public sealed class CommerceCheckoutSession
{
    private CommerceCheckoutSession() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string RequestProtected { get; private set; } = "";
    public string? ReservationProtected { get; private set; }
    public string? ReservationReference { get; private set; }
    public CommerceSessionStatus Status { get; private set; }
    public Guid? CommerceOrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PayableUntil { get; private set; }
    public uint Version { get; private set; }
    public static CommerceCheckoutSession Create(Guid user, string provider, string key, string fingerprint, string request)
        => new() { Id = Guid.NewGuid(), UserId = user, ProviderKey = provider, IdempotencyKey = key,
            Fingerprint = fingerprint, RequestProtected = request, Status = CommerceSessionStatus.Reserving, CreatedAt = DateTimeOffset.UtcNow };
    public void Reserved(string reference, string reservation, DateTimeOffset until)
    {
        if (Status != CommerceSessionStatus.Reserving) throw new CommerceDomainException("وضعیت رزرو تغییر کرده است", "SESSION_STATE");
        ReservationReference = reference; ReservationProtected = reservation; PayableUntil = until; Status = CommerceSessionStatus.Reserved;
    }
    public void AttachOrder(Guid id) { CommerceOrderId = id; Status = CommerceSessionStatus.Confirmed; }
    public void Releasing()
    {
        if (CommerceOrderId.HasValue) throw new CommerceDomainException("رزرو به سفارش متصل است", "SESSION_ATTACHED");
        Status = CommerceSessionStatus.Releasing;
    }
    public void Released() => Status = CommerceSessionStatus.Released;
    public void Unknown() => Status = CommerceSessionStatus.Unknown;
    public void RedactPrivateData()
    {
        if (Status is not (CommerceSessionStatus.Confirmed or CommerceSessionStatus.Released or CommerceSessionStatus.Expired))
            throw new CommerceDomainException("اطلاعات رزرو فعال قابل حذف نیست", "SESSION_RETENTION_NOT_REACHED");
        RequestProtected = string.Empty; ReservationProtected = null;
    }
}

public sealed class CommerceCatalogSnapshot
{
    private CommerceCatalogSnapshot() { }
    public string AccountKey { get; private set; } = "";
    public string PayloadJson { get; private set; } = "[]";
    public DateTimeOffset PublishedAt { get; private set; }
    public static CommerceCatalogSnapshot Create(string key, string payload) => new() { AccountKey = key, PayloadJson = payload, PublishedAt = DateTimeOffset.UtcNow };
    public void Publish(string payload) { PayloadJson = payload; PublishedAt = DateTimeOffset.UtcNow; }
}

/// <summary>A durable encrypted provider response checkpoint, saved before business mapping.</summary>
public sealed class CommerceProviderReceipt
{
    private CommerceProviderReceipt() { }
    public string OperationId { get; private set; } = "";
    public string AccountKey { get; private set; } = "";
    public string PayloadProtected { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    public static CommerceProviderReceipt Create(string operation, string account, string payload) => new()
    { OperationId = operation, AccountKey = account, PayloadProtected = payload, UpdatedAt = DateTimeOffset.UtcNow };
    public void Update(string payload) { PayloadProtected = payload; UpdatedAt = DateTimeOffset.UtcNow; }
}
