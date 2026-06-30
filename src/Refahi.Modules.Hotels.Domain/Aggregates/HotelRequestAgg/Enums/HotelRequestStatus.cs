namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;

public enum HotelRequestStatus
{
    Created = 1,
    ConvertedToOrder = 2,
    Expired = 3,
    Cancelled = 4,
    ProviderConfirmed = 5,
    Completed = 6,
    Failed = 7
}
