using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.DomainEvents;

public sealed class ProvisionalBookingCreatedEvent : IDomainEvent
{
    public BookingId BookingId { get; }
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public ProvisionalBookingCreatedEvent(BookingId bookingId)
    {
        BookingId = bookingId;
    }
}
