using Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums;

namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg;

public sealed class HotelRequest
{
    private HotelRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public HotelRequestStatus Status { get; private set; }
    public DateTime ExpireAt { get; private set; }

    public string ProviderName { get; private set; } = string.Empty;
    public long ProviderHotelId { get; private set; }
    public long ProviderRoomId { get; private set; }
    public string? ProviderBookingCode { get; private set; }
    public DateTime? ProviderConfirmedAt { get; private set; }

    public string SearchCriteriaSnapshot { get; private set; } = "{}";
    public string SelectedHotelSnapshot { get; private set; } = "{}";
    public string SelectedRoomSnapshot { get; private set; } = "{}";

    public long TotalPrice { get; private set; }
    public string Currency { get; private set; } = "IRR";
    public string Breakdown { get; private set; } = "{}";
    public string? Fees { get; private set; }

    public string GuestInfoSnapshot { get; private set; } = "{}";

    public Guid? OrderId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public static HotelRequest Create(
        Guid userId,
        string providerName,
        long providerHotelId,
        long providerRoomId,
        string searchCriteriaSnapshot,
        string selectedHotelSnapshot,
        string selectedRoomSnapshot,
        long totalPrice,
        string currency,
        string breakdown,
        string? fees,
        string guestInfoSnapshot,
        DateTime nowUtc,
        DateTime expireAtUtc,
        string idempotencyKey)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId is required.");
        if (providerHotelId <= 0)
            throw new DomainException("Provider hotel id is required.");
        if (providerRoomId <= 0)
            throw new DomainException("Provider room id is required.");
        if (totalPrice <= 0)
            throw new DomainException("Total price must be greater than zero.");
        if (expireAtUtc <= nowUtc)
            throw new DomainException("ExpireAt must be in the future.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency key is required.");

        return new HotelRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProviderName = string.IsNullOrWhiteSpace(providerName) ? "SnappTrip" : providerName.Trim(),
            ProviderHotelId = providerHotelId,
            ProviderRoomId = providerRoomId,
            SearchCriteriaSnapshot = NormalizeJson(searchCriteriaSnapshot),
            SelectedHotelSnapshot = NormalizeJson(selectedHotelSnapshot),
            SelectedRoomSnapshot = NormalizeJson(selectedRoomSnapshot),
            TotalPrice = totalPrice,
            Currency = string.IsNullOrWhiteSpace(currency) ? "IRR" : currency.Trim().ToUpperInvariant(),
            Breakdown = NormalizeJson(breakdown),
            Fees = string.IsNullOrWhiteSpace(fees) ? null : NormalizeJson(fees),
            GuestInfoSnapshot = NormalizeJson(guestInfoSnapshot),
            Status = HotelRequestStatus.Created,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            ExpireAt = expireAtUtc,
            IdempotencyKey = idempotencyKey.Trim()
        };
    }

    public void MarkExpired(DateTime nowUtc)
    {
        if (Status == HotelRequestStatus.Expired)
            return;

        if (Status != HotelRequestStatus.Created)
            throw new DomainException($"Cannot expire hotel request from {Status} state.");

        Status = HotelRequestStatus.Expired;
        UpdatedAt = nowUtc;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (Status == HotelRequestStatus.Cancelled)
            return;

        if (Status != HotelRequestStatus.Created)
            throw new DomainException($"Cannot cancel hotel request from {Status} state.");

        Status = HotelRequestStatus.Cancelled;
        UpdatedAt = nowUtc;
    }

    public void ConvertToOrder(Guid orderId, DateTime nowUtc)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("OrderId is required.");
        if (Status == HotelRequestStatus.Expired || ExpireAt <= nowUtc)
            throw new DomainException("Hotel request is expired.");
        if (Status == HotelRequestStatus.Cancelled)
            throw new DomainException("Hotel request is cancelled.");
        if (Status == HotelRequestStatus.ConvertedToOrder)
        {
            if (OrderId == orderId)
                return;

            throw new DomainException("Hotel request is already converted to order.");
        }

        Status = HotelRequestStatus.ConvertedToOrder;
        OrderId = orderId;
        UpdatedAt = nowUtc;
    }

    public void MarkProviderConfirmed(string providerBookingCode, DateTime nowUtc)
    {
        if (Status == HotelRequestStatus.ProviderConfirmed || Status == HotelRequestStatus.Completed)
            return;

        if (Status != HotelRequestStatus.ConvertedToOrder || OrderId is null)
            throw new DomainException("Hotel request must be converted to order before provider confirmation.");

        ProviderBookingCode = string.IsNullOrWhiteSpace(providerBookingCode)
            ? null
            : providerBookingCode.Trim();
        ProviderConfirmedAt = nowUtc;
        Status = HotelRequestStatus.ProviderConfirmed;
        UpdatedAt = nowUtc;
    }

    public void Complete(DateTime nowUtc)
    {
        if (Status == HotelRequestStatus.Completed)
            return;

        if (Status != HotelRequestStatus.ProviderConfirmed)
            throw new DomainException("Hotel request must be provider-confirmed before completion.");

        Status = HotelRequestStatus.Completed;
        UpdatedAt = nowUtc;
    }

    public void MarkFailed(DateTime nowUtc)
    {
        if (Status == HotelRequestStatus.Failed)
            return;

        if (Status is HotelRequestStatus.ProviderConfirmed or HotelRequestStatus.Completed)
            throw new DomainException($"Cannot fail hotel request from {Status} state.");

        Status = HotelRequestStatus.Failed;
        UpdatedAt = nowUtc;
    }

    private static string NormalizeJson(string? value)
        => string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();
}
