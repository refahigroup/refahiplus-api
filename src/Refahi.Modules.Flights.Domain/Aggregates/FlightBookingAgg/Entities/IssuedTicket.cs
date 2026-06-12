using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;

public sealed class IssuedTicket
{
    private IssuedTicket()
    {
        TicketNumber = string.Empty;
        PassengerNameSnapshot = string.Empty;
    }

    public IssuedTicket(
        Guid passengerId,
        string ticketNumber,
        string passengerNameSnapshot,
        DateTime issuedAtUtc,
        string? providerTicketId = null,
        string? providerTraceId = null,
        string? snapshotJson = null)
    {
        if (passengerId == Guid.Empty)
        {
            throw new DomainException("Passenger id is required for issued ticket.");
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            throw new DomainException("Ticket number is required.");
        }

        if (string.IsNullOrWhiteSpace(passengerNameSnapshot))
        {
            throw new DomainException("Passenger name snapshot is required.");
        }

        PassengerId = passengerId;
        TicketNumber = ticketNumber.Trim();
        PassengerNameSnapshot = passengerNameSnapshot.Trim();
        IssuedAtUtc = issuedAtUtc;
        ProviderTicketId = string.IsNullOrWhiteSpace(providerTicketId) ? null : providerTicketId.Trim();
        ProviderTraceId = string.IsNullOrWhiteSpace(providerTraceId) ? null : providerTraceId.Trim();
        SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? null : snapshotJson.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PassengerId { get; private set; }
    public string TicketNumber { get; private set; }
    public string PassengerNameSnapshot { get; private set; }
    public string? ProviderTicketId { get; private set; }
    public string? ProviderTraceId { get; private set; }
    public string? SnapshotJson { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
}
