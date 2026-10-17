namespace Refahi.Modules.Pools.Domain;

public sealed class PoolProviderAttempt
{
    private PoolProviderAttempt() { }
    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string Operation { get; private set; } = "";
    public string Outcome { get; private set; } = "Started";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

}
