using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.DomainEvents;

public sealed class BookingProviderConfirmationFailedEvent : IDomainEvent
{
    public BookingId BookingId { get; }
    public string Reason { get; }
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public BookingProviderConfirmationFailedEvent(BookingId bookingId, string reason)
    {
        BookingId = bookingId;
        Reason = reason;
    }
}
