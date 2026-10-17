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
    // Historical storage only. New purchases use PoolPurchase; no legacy lifecycle is registered.
    public bool HasTicket => PaymentStatus == "Paid" && IssuanceStatus == "Issued" && CancellationStatus == "None";
}
