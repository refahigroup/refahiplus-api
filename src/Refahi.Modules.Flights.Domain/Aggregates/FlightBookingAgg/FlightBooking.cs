using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Entities;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Exceptions;

namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg;

public sealed class FlightBooking
{
    public const string CategoryCode = "flight";

    private readonly List<Passenger> _passengers = new();
    private readonly List<FlightSegment> _segments = new();
    private readonly List<IssuedTicket> _issuedTickets = new();
    private readonly List<CancellationRequest> _cancellationRequests = new();

    private FlightBooking()
    {
        Provider = null!;
        SelectedFare = null!;
        Contact = null!;
        FareBreakdown = null!;
    }

    public FlightBookingId Id { get; private set; }
    public Guid UserId { get; private set; }
    public FlightBookingStatus Status { get; private set; }
    public ProviderSnapshot Provider { get; private set; }
    public SelectedFareSnapshot SelectedFare { get; private set; }
    public ProviderBookingSnapshot? ProviderBooking { get; private set; }
    public ContactInfo Contact { get; private set; }
    public FareBreakdown FareBreakdown { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? OrderId { get; private set; }
    public string? OrderNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string? IssueFailureReason { get; private set; }
    public CancellationQuoteSnapshot? LatestCancellationQuote { get; private set; }

    public IReadOnlyCollection<Passenger> Passengers => _passengers.AsReadOnly();
    public IReadOnlyCollection<FlightSegment> Segments => _segments.AsReadOnly();
    public IReadOnlyCollection<IssuedTicket> IssuedTickets => _issuedTickets.AsReadOnly();
    public IReadOnlyCollection<CancellationRequest> CancellationRequests => _cancellationRequests.AsReadOnly();

    public static FlightBooking CreateDraft(
        FlightBookingId id,
        Guid userId,
        ProviderSnapshot provider,
        SelectedFareSnapshot selectedFare,
        ContactInfo contact,
        IEnumerable<Passenger> passengers,
        IEnumerable<FlightSegment> segments,
        FareBreakdown fareBreakdown,
        string idempotencyKey,
        DateTime nowUtc,
        DateTime? expiresAtUtc = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (passengers is null)
        {
            throw new DomainException("Passengers are required.");
        }

        if (segments is null)
        {
            throw new DomainException("Flight segments are required.");
        }

        var passengerList = passengers.ToList();
        if (passengerList.Count == 0)
        {
            throw new DomainException("At least one passenger is required.");
        }

        var segmentList = segments.OrderBy(segment => segment.Sequence).ToList();
        if (segmentList.Count == 0)
        {
            throw new DomainException("At least one flight segment is required.");
        }

        EnsureUniquePassengerIds(passengerList);
        EnsureUniqueSegmentSequences(segmentList);

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= nowUtc)
        {
            throw new DomainException("Flight booking expiration must be after creation time.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Flight booking idempotency key is required.");
        }

        var booking = new FlightBooking
        {
            Id = id,
            UserId = userId,
            Provider = provider,
            SelectedFare = selectedFare,
            Contact = contact,
            FareBreakdown = fareBreakdown,
            IdempotencyKey = idempotencyKey.Trim(),
            Status = FlightBookingStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc
        };

        booking._passengers.AddRange(passengerList);
        booking._segments.AddRange(segmentList);

        return booking;
    }

    public void MarkProviderBooked(ProviderBookingSnapshot providerBooking, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.Draft, "Only draft flight booking can be marked provider booked.");

        ProviderBooking = providerBooking;
        Status = FlightBookingStatus.ProviderBooked;
        UpdatedAtUtc = nowUtc;
    }

    public void AttachOrder(Guid orderId, string orderNumber, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.ProviderBooked, "Order can only be attached after provider booking.");

        if (orderId == Guid.Empty)
        {
            throw new DomainException("Order id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("Order number is required.");
        }

        OrderId = orderId;
        OrderNumber = orderNumber.Trim();
        Status = FlightBookingStatus.OrderCreated;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkPaymentPending(DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.OrderCreated, "Payment can only become pending after order creation.");

        Status = FlightBookingStatus.PaymentPending;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkPaid(DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.PaymentPending, "Flight booking can only be paid from payment pending state.");

        Status = FlightBookingStatus.Paid;
        UpdatedAtUtc = nowUtc;
    }

    public void StartIssuing(DateTime nowUtc)
    {
        if (Status is not FlightBookingStatus.Paid and not FlightBookingStatus.IssueFailed)
        {
            throw new DomainException("Issuing can only start after payment.");
        }

        IssueFailureReason = null;
        Status = FlightBookingStatus.Issuing;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkIssued(IEnumerable<IssuedTicket> issuedTickets, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.Issuing, "Tickets can only be issued from issuing state.");

        if (issuedTickets is null)
        {
            throw new DomainException("Issued tickets are required.");
        }

        var ticketList = issuedTickets.ToList();
        if (ticketList.Count == 0)
        {
            throw new DomainException("At least one issued ticket is required.");
        }

        EnsureTicketsBelongToPassengers(ticketList);

        _issuedTickets.Clear();
        _issuedTickets.AddRange(ticketList);
        IssueFailureReason = null;
        Status = FlightBookingStatus.Issued;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkIssueFailed(string reason, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.Issuing, "Issue failure can only be recorded from issuing state.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Issue failure reason is required.");
        }

        IssueFailureReason = reason.Trim();
        Status = FlightBookingStatus.IssueFailed;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkExpired(DateTime nowUtc)
    {
        if (Status is not FlightBookingStatus.Draft
            and not FlightBookingStatus.ProviderBooked
            and not FlightBookingStatus.OrderCreated
            and not FlightBookingStatus.PaymentPending)
        {
            throw new DomainException("Only unpaid flight booking can expire.");
        }

        Status = FlightBookingStatus.Expired;
        UpdatedAtUtc = nowUtc;
    }

    public void QuoteCancellation(CancellationQuoteSnapshot quote, DateTime nowUtc)
    {
        if (Status is not FlightBookingStatus.Issued and not FlightBookingStatus.CancellationFailed)
        {
            throw new DomainException("Cancellation can only be quoted for issued flight booking.");
        }

        LatestCancellationQuote = quote;
        Status = FlightBookingStatus.CancellationQuoted;
        UpdatedAtUtc = nowUtc;
    }

    public void RequestCancellation(Guid cancellationRequestId, string reason, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.CancellationQuoted, "Cancellation can only be requested after quote.");

        if (LatestCancellationQuote is null)
        {
            throw new DomainException("Cancellation quote is required before request.");
        }

        if (LatestCancellationQuote.ExpiresAtUtc.HasValue && LatestCancellationQuote.ExpiresAtUtc.Value <= nowUtc)
        {
            throw new DomainException("Cancellation quote is expired.");
        }

        var request = new CancellationRequest(cancellationRequestId, LatestCancellationQuote, reason, nowUtc);
        _cancellationRequests.Add(request);

        Status = FlightBookingStatus.CancellationRequested;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancellationFailed(Guid cancellationRequestId, string reason, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.CancellationRequested, "Cancellation can only fail from requested state.");

        var request = GetCancellationRequest(cancellationRequestId);
        request.MarkFailed(reason, nowUtc);

        Status = FlightBookingStatus.CancellationFailed;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(Guid cancellationRequestId, string? providerCancellationId, DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.CancellationRequested, "Flight booking can only be cancelled from requested state.");

        var request = GetCancellationRequest(cancellationRequestId);
        request.MarkCancelled(providerCancellationId, nowUtc);

        Status = FlightBookingStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRefundPending(DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.Cancelled, "Refund can only become pending after cancellation.");

        Status = FlightBookingStatus.RefundPending;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRefunded(DateTime nowUtc)
    {
        EnsureStatus(FlightBookingStatus.RefundPending, "Flight booking can only be refunded from refund pending state.");

        Status = FlightBookingStatus.Refunded;
        UpdatedAtUtc = nowUtc;
    }

    private void EnsureStatus(FlightBookingStatus expectedStatus, string message)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException(message);
        }
    }

    private CancellationRequest GetCancellationRequest(Guid cancellationRequestId)
    {
        var request = _cancellationRequests.SingleOrDefault(item => item.Id == cancellationRequestId);
        if (request is null)
        {
            throw new DomainException("Cancellation request was not found.");
        }

        return request;
    }

    private void EnsureTicketsBelongToPassengers(IEnumerable<IssuedTicket> issuedTickets)
    {
        var passengerIds = _passengers.Select(passenger => passenger.Id).ToHashSet();
        foreach (var ticket in issuedTickets)
        {
            if (!passengerIds.Contains(ticket.PassengerId))
            {
                throw new DomainException("Issued ticket passenger does not belong to booking.");
            }
        }
    }

    private static void EnsureUniquePassengerIds(IEnumerable<Passenger> passengers)
    {
        var duplicateExists = passengers
            .GroupBy(passenger => passenger.Id)
            .Any(group => group.Count() > 1);

        if (duplicateExists)
        {
            throw new DomainException("Passenger ids must be unique.");
        }
    }

    private static void EnsureUniqueSegmentSequences(IEnumerable<FlightSegment> segments)
    {
        var duplicateExists = segments
            .GroupBy(segment => segment.Sequence)
            .Any(group => group.Count() > 1);

        if (duplicateExists)
        {
            throw new DomainException("Flight segment sequences must be unique.");
        }
    }
}
