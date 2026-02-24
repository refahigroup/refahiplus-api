using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.DomainEvents;

public sealed class BookingConfirmedEvent : IDomainEvent
{
    public BookingId BookingId { get; }
    public DateTimeOffset OccurredAt { get; } = DateTime.UtcNow;

    public BookingConfirmedEvent(BookingId bookingId)
    {
        BookingId = bookingId;
    }
}