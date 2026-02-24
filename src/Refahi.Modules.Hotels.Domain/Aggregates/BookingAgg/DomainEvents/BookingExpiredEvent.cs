using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.DomainEvents;

public sealed class BookingExpiredEvent : IDomainEvent
{
    public BookingId BookingId { get; }
    public DateTimeOffset OccurredAt { get; } = DateTime.UtcNow;

    public BookingExpiredEvent(BookingId bookingId)
    {
        BookingId = bookingId;
    }
}