namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

public enum HotelProviderBookingStatus
{
    None = 0,
    Started = 1,
    Confirmed = 2,
    Failed = 3,
    CancellationPending = 4,
    Cancelled = 5,
    ExternallyUnresolved = 6
}
