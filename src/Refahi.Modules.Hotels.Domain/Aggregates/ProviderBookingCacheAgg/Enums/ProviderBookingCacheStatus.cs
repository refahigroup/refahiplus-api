namespace Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg.Enums;

public enum ProviderBookingCacheStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    CancellationPending = 4,
    Cancelled = 5,
    ExternallyUnresolved = 6
}
