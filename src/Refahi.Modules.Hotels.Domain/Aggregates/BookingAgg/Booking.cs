using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.DomainEvents;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.ValueObjects;
using Refahi.Shared.Domain;

namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg;

public sealed class Booking
{
    private readonly List<Guest> _guests = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    private Booking()
    {
    }

    public BookingId Id { get; private set; }

    public ProviderType Provider { get; private set; }

    public ProviderBookingCode ProviderBookingCode { get; private set; }
    public ProviderHotelId ProviderHotelId { get; private set; }
    public ProviderRoomId ProviderRoomId { get; private set; }

    public DateRange StayRange { get; private set; }

    public IReadOnlyCollection<Guest> Guests => _guests.AsReadOnly();

    public int RoomsCount { get; private set; }
    public RoomBoardType BoardType { get; private set; }

    public Money BasePrice { get; private set; }      // provider price
    public Money MarginAmount { get; private set; }   // our margin
    public Money CustomerPrice { get; private set; }  // final price

    public BookingStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public DateTime? LockedUntil { get; private set; }

    public Voucher? Voucher { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private void AddDomainEvent(IDomainEvent @event)
    {
        _domainEvents.Add(@event);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    #region Factory

    public static Booking CreateProvisional(
        BookingId id,
        ProviderType provider,
        ProviderBookingCode providerCode,
        ProviderHotelId hotelId,
        ProviderRoomId roomId,
        DateRange stayRange,
        IEnumerable<Guest> guests,
        int roomsCount,
        RoomBoardType boardType,
        Money basePrice,
        Money margin,
        Money customerPrice,
        DateTime nowUtc,
        DateTime? lockedUntilUtc = null)
    {
        if (guests == null) throw new DomainException("Guests are required.");

        var guestList = guests.ToList();
        if (guestList.Count == 0)
            throw new DomainException("At least one guest is required.");

        if (roomsCount <= 0)
            throw new DomainException("Rooms count must be greater than zero.");

        if (!string.Equals(basePrice.Currency, customerPrice.Currency, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Currency mismatch between base and customer price.");

        var booking = new Booking
        {
            Id = id,
            Provider = provider,
            ProviderBookingCode = providerCode,
            ProviderHotelId = hotelId,
            ProviderRoomId = roomId,
            StayRange = stayRange,
            RoomsCount = roomsCount,
            BoardType = boardType,
            BasePrice = basePrice,
            MarginAmount = margin,
            CustomerPrice = customerPrice,
            Status = BookingStatus.Provisional,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            LockedUntil = lockedUntilUtc
        };

        booking._guests.AddRange(guestList);
        booking.AddDomainEvent(new ProvisionalBookingCreatedEvent(booking.Id));

        return booking;
    }

    #endregion

    #region State Transitions

    public void MarkPaymentPending(DateTime nowUtc)
    {
        if (Status != BookingStatus.Provisional)
            throw new DomainException("Only provisional booking can enter payment phase.");

        Status = BookingStatus.PaymentPending;
        UpdatedAt = nowUtc;
    }

    public void MarkPaymentFailed(DateTime nowUtc)
    {
        if (Status != BookingStatus.PaymentPending &&
            Status != BookingStatus.Provisional)
            throw new DomainException("Payment can only fail from provisional or pending state.");

        Status = BookingStatus.PaymentFailed;
        UpdatedAt = nowUtc;

        AddDomainEvent(new BookingPaymentFailedEvent(Id));
    }

    public void MarkPaymentSucceeded(DateTime nowUtc)
    {
        if (Status != BookingStatus.PaymentPending)
            throw new DomainException("Cannot succeed payment before entering payment pending state.");

        Status = BookingStatus.ConfirmingProvider;
        UpdatedAt = nowUtc;

        AddDomainEvent(new BookingPaymentSucceededEvent(Id));
    }

    public void Confirm(Voucher voucher, DateTime nowUtc)
    {
        if (Status != BookingStatus.ConfirmingProvider)
            throw new DomainException("Booking is not in confirming state.");

        Status = BookingStatus.Confirmed;
        Voucher = voucher;
        UpdatedAt = nowUtc;

        AddDomainEvent(new BookingConfirmedEvent(Id));
    }

    public void ConfirmFailed(string reason, DateTime nowUtc)
    {
        if (Status != BookingStatus.ConfirmingProvider)
            throw new DomainException("Booking is not in confirming state.");

        Status = BookingStatus.ConfirmFailed;
        UpdatedAt = nowUtc;

        AddDomainEvent(new BookingProviderConfirmationFailedEvent(Id, reason));
    }

    public void MarkExpired(DateTime nowUtc)
    {
        if (Status != BookingStatus.Provisional &&
            Status != BookingStatus.PaymentPending)
        {
            // only provisional or paymentPending can expire; ignore others
            return;
        }

        Status = BookingStatus.Expired;
        UpdatedAt = nowUtc;

        AddDomainEvent(new BookingExpiredEvent(Id));
    }

    #endregion
}
