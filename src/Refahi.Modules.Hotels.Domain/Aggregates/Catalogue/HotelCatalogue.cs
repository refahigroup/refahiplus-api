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
    public Guid? CityKey { get; private set; }
    public int? ReferenceCityId { get; private set; }
    public string Provider { get; private set; } = "";
    public string NativeId { get; private set; } = "";
    public string NativeCityId { get; private set; } = "";
    public static HotelListing Create(int referenceCityId, string provider, string hotel, string nativeCity)
    {
        if (referenceCityId <= 0) throw new ArgumentOutOfRangeException(nameof(referenceCityId));
        return new() { HotelKey = Guid.NewGuid(), ReferenceCityId = referenceCityId, Provider = provider, NativeId = hotel, NativeCityId = nativeCity };
    }
    public void AttachReferenceCity(int cityId)
    {
        if (cityId <= 0) throw new ArgumentOutOfRangeException(nameof(cityId));
        ReferenceCityId = cityId;
    }
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
public enum HotelExternalState { Prepared, Submitting, Reserved, Confirming, Confirmed, Unknown, Failed, Expired, Cancelled }
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
    public long HotelShareMinor { get; private set; }
    public bool PaymentBlocked { get; private set; }
    public string? ReviewReason { get; private set; }
    public bool ConfirmationAttempted { get; private set; }
    public bool NoExternalLiability { get; private set; }
    public DateTime NextRecoveryAtUtc { get; private set; } = DateTime.UtcNow;
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
    public void ScheduleRecovery(int seconds) { NextRecoveryAtUtc = DateTime.UtcNow.AddSeconds(seconds); Version++; }
    public void StopRecovery() { NextRecoveryAtUtc = DateTime.MaxValue; Touch(); }
    public void BlockPayment(string reason) { PaymentBlocked = true; ReviewReason = reason; Touch(); }
    public void StartSubmit() { if (State != HotelExternalState.Prepared) throw new InvalidOperationException("رزرو قبلاً ارسال شده است."); State = HotelExternalState.Submitting; Touch(); }
    public void Reserve(long price, long fee, DateTime until, string snapshot)
    {
        if (State != HotelExternalState.Submitting || price <= 0 || fee < 0 || fee > price || until <= DateTime.UtcNow)
            throw new InvalidOperationException("پاسخ رزرو معتبر نیست.");
        SupplyPriceMinor = price; FeeMinor = fee; HotelShareMinor = price - fee; LockedUntilUtc = until; ResponseSnapshot = snapshot;
        State = HotelExternalState.Reserved; Touch();
    }
    public void StartConfirm() { if (State != HotelExternalState.Reserved || PaymentBlocked || NoExternalLiability) throw new InvalidOperationException("رزرو قابل تأیید نیست."); State = HotelExternalState.Confirming; ConfirmationAttempted = true; Touch(); }
    public void Confirm(string id) { if (State != HotelExternalState.Confirming || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("تأیید رزرو معتبر نیست."); ReservationId = id; State = HotelExternalState.Confirmed; Touch(); }
    public void MarkUnknown(string? reason = null)
    {
        if (State is HotelExternalState.Confirmed or HotelExternalState.Cancelled || NoExternalLiability) return;
        State = HotelExternalState.Unknown; ReviewReason = reason ?? "EXTERNAL_UNKNOWN"; Touch();
    }
    public void Fail(string? reason = null)
    {
        if (State != HotelExternalState.Submitting) throw new InvalidOperationException("شکست قطعی رزرو قابل ثبت نیست.");
        State = HotelExternalState.Failed; NoExternalLiability = true; ReviewReason = reason; Touch();
    }
    public void Expire() { if (State is HotelExternalState.Prepared or HotelExternalState.Reserved) { State = HotelExternalState.Expired; Touch(); } }
    public void ReconcileReserved(long price, long fee, DateTime until, string snapshot, bool safeToConfirmAgain)
    {
        if (State != HotelExternalState.Unknown || NoExternalLiability || ConfirmationAttempted && !safeToConfirmAgain
            || price <= 0 || fee < 0 || fee > price || until <= DateTime.UtcNow) throw new InvalidOperationException("وضعیت استعلام اجازه ادامه رزرو نمی‌دهد.");
        SupplyPriceMinor = price; FeeMinor = fee; HotelShareMinor = price - fee; LockedUntilUtc = until; ResponseSnapshot = snapshot;
        State = HotelExternalState.Reserved; Touch();
    }
    public void ReconcileConfirmed(string id, string snapshot)
    {
        if (State != HotelExternalState.Unknown || NoExternalLiability || string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("نتیجه استعلام معتبر نیست.");
        ReservationId = id; ResponseSnapshot = snapshot; State = HotelExternalState.Confirmed; Touch();
    }
    public void ReconcileNoLiability(bool cancelled, string snapshot)
    {
        if (State != HotelExternalState.Unknown) throw new InvalidOperationException("وضعیت استعلام قابل اعمال نیست.");
        State = cancelled ? HotelExternalState.Cancelled : HotelExternalState.Failed;
        NoExternalLiability = true; PaymentBlocked = true; ResponseSnapshot = snapshot; Touch();
    }
}
