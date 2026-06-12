using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightBookingLifecycleTests
{
    [Fact]
    public void Lifecycle_MovesThroughOrderPaymentAndIssueStates()
    {
        var now = DateTime.UtcNow;
        var booking = FlightBookingTestFactory.CreateDraft(now);
        var passenger = booking.Passengers.Single();

        booking.MarkProviderBooked(new ProviderBookingSnapshot("book-1", "track-1", now.AddMinutes(1)), now.AddMinutes(1));
        booking.AttachOrder(Guid.NewGuid(), "ORD-1", now.AddMinutes(2));
        booking.MarkPaymentPending(now.AddMinutes(3));
        booking.MarkPaid(now.AddMinutes(4));
        booking.StartIssuing(now.AddMinutes(5));
        booking.MarkIssued(
            [new IssuedTicket(passenger.Id, "ticket-1", passenger.DisplayName, now.AddMinutes(6))],
            now.AddMinutes(6));

        Assert.Equal(FlightBookingStatus.Issued, booking.Status);
        Assert.Single(booking.IssuedTickets);
    }

    [Fact]
    public void AttachOrder_RejectsDraftBooking()
    {
        var booking = FlightBookingTestFactory.CreateDraft(DateTime.UtcNow);

        var ex = Assert.Throws<DomainException>(() =>
            booking.AttachOrder(Guid.NewGuid(), "ORD-1", DateTime.UtcNow));

        Assert.Contains("provider booking", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
