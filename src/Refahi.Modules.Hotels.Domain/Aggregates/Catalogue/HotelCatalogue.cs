namespace Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

public sealed class HotelCityReference
{
    private HotelCityReference() { }
    public Guid Id { get; private set; }
    public Guid CityKey { get; private set; }
    public string Provider { get; private set; } = "";
    public string NativeId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int? LegacyId { get; private set; }
    public static HotelCityReference Create(Guid key, string provider, string nativeId, string name, int? legacyId) =>
        new() { Id = Guid.NewGuid(), CityKey = key, Provider = provider, NativeId = nativeId, Name = name, LegacyId = legacyId };
}
public sealed class HotelListing
{
    private HotelListing() { }
    public Guid HotelKey { get; private set; }
    public Guid CityKey { get; private set; }
    public string Provider { get; private set; } = "";
    public string NativeId { get; private set; } = "";
    public string NativeCityId { get; private set; } = "";
    public static HotelListing Create(Guid city, string provider, string hotel, string nativeCity) =>
        new() { HotelKey = Guid.NewGuid(), CityKey = city, Provider = provider, NativeId = hotel, NativeCityId = nativeCity };
}
public sealed class HotelOfferSnapshot
{
    private HotelOfferSnapshot() { }
    public string Token { get; private set; } = "";
    public Guid HotelKey { get; private set; }
    public string Provider { get; private set; } = "";
    public string OfferJson { get; private set; } = "{}";
    public string TravelJson { get; private set; } = "{}";
    public DateTime ExpiresAtUtc { get; private set; }
    public static HotelOfferSnapshot Create(string token, Guid hotel, string provider, string offer, string travel, DateTime expiry) =>
        new() { Token = token, HotelKey = hotel, Provider = provider, OfferJson = offer, TravelJson = travel, ExpiresAtUtc = expiry };
}
public enum HotelExternalState { Prepared, Submitting, Reserved, Confirming, Confirmed, Unknown, Failed, Expired }
public sealed class HotelExternalReservation
{
    private HotelExternalReservation() { }
    public Guid HotelRequestId { get; private set; }
    public string Provider { get; private set; } = "";
    public string ReferenceId { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string SubmitJson { get; private set; } = "{}";
    public HotelExternalState State { get; private set; }
    public long SupplyPriceMinor { get; private set; }
    public long FeeMinor { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public string? ReservationId { get; private set; }
    public string? ResponseSnapshot { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public int Version { get; private set; }
    public static HotelExternalReservation Create(Guid request, string provider, string hash, string json) =>
        new()
        {
            HotelRequestId = request,
            Provider = provider,
            ReferenceId = request.ToString("N"),
            RequestHash = hash,
            SubmitJson = json,
            State = HotelExternalState.Prepared,
            UpdatedAtUtc = DateTime.UtcNow
        };
    private void Touch() { UpdatedAtUtc = DateTime.UtcNow; Version++; }
    public void StartSubmit() { if (State != HotelExternalState.Prepared) throw new InvalidOperationException("رزرو قبلاً ارسال شده است."); State = HotelExternalState.Submitting; Touch(); }
    public void Reserve(long price, long fee, DateTime until, string snapshot)
    {
        if (State != HotelExternalState.Submitting || price <= 0 || fee < 0 || until <= DateTime.UtcNow)
            throw new InvalidOperationException("پاسخ رزرو معتبر نیست.");
        SupplyPriceMinor = price; FeeMinor = fee; LockedUntilUtc = until; ResponseSnapshot = snapshot;
        State = HotelExternalState.Reserved; Touch();
    }
    public void StartConfirm() { if (State != HotelExternalState.Reserved) throw new InvalidOperationException("رزرو قابل تأیید نیست."); State = HotelExternalState.Confirming; Touch(); }
    public void Confirm(string id) { if (State != HotelExternalState.Confirming || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("تأیید رزرو معتبر نیست."); ReservationId = id; State = HotelExternalState.Confirmed; Touch(); }
    public void MarkUnknown() { State = HotelExternalState.Unknown; Touch(); }
    public void Fail() { State = HotelExternalState.Failed; Touch(); }
    public void Expire() { if (State is HotelExternalState.Prepared or HotelExternalState.Reserved) { State = HotelExternalState.Expired; Touch(); } }
}
