namespace Refahi.Modules.Pools.Domain;

public enum PoolPurchaseStatus { AwaitingPayment, Paid, CancellationPending, Cancelled, Refunded }
public enum PoolVoucherStatus { Issued, Redeemed, Revoked, Expired }
public enum PoolReservationStatus { ExternalReservationBypassed }

public sealed class PoolPurchase
{
    private PoolPurchase() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public Guid? OrderId { get; private set; }
    public string? OrderNumber { get; private set; }
    public long TotalMinor { get; private set; }
    public DateTimeOffset PayableUntil { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public long Version { get; private set; } = 1;
    public PoolPurchaseStatus Status { get; private set; }
    private readonly List<PoolRequest> _requests = [];
    public IReadOnlyCollection<PoolRequest> Requests => _requests;
    public bool CanPay(DateTimeOffset now) => Status == PoolPurchaseStatus.AwaitingPayment && PayableUntil > now;

    public static PoolPurchase Create(Guid userId, string key, string fingerprint, IReadOnlyList<PoolRequest> requests,
        DateTimeOffset now, DateTimeOffset payableUntil)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 128 || requests.Count is < 1 or > 20
            || requests.Select(x => x.SessionId).Distinct().Count() != requests.Count || requests.Any(x => x.PurchaseId != Guid.Empty || x.StartsAtUtc <= now) || payableUntil <= now || payableUntil > requests.Min(x => x.StartsAtUtc))
            throw new InvalidOperationException("اطلاعات خرید یا مهلت پرداخت معتبر نیست");
        var total = requests.Aggregate(0L, (sum, request) => checked(sum + request.TotalMinor));
        var purchase = new PoolPurchase { Id = Guid.NewGuid(), UserId = userId, IdempotencyKey = key,
            Fingerprint = fingerprint, CreatedAt = now, PayableUntil = payableUntil };
        foreach (var request in requests) request.AssignTo(purchase.Id);
        purchase._requests.AddRange(requests);
        purchase.TotalMinor = total;
        return purchase;
    }

    public void AttachOrder(Guid orderId, string number)
    {
        if (orderId == Guid.Empty || OrderId.HasValue && OrderId != orderId) throw new InvalidOperationException("سفارش مالی معتبر نیست");
        if (OrderId == orderId) return;
        OrderId = orderId; OrderNumber = number; Version++;
    }

    public void CommitPaid(IReadOnlyDictionary<Guid, (string Hash, string Ciphertext)> codes, DateTimeOffset now)
    {
        if (Status == PoolPurchaseStatus.Paid) return;
        if (Status != PoolPurchaseStatus.AwaitingPayment || !OrderId.HasValue || codes.Count != _requests.Count || _requests.Any(x => !codes.TryGetValue(x.Id, out var c) || string.IsNullOrWhiteSpace(c.Hash) || string.IsNullOrWhiteSpace(c.Ciphertext)))
            throw new InvalidOperationException("صدور ووچر در این وضعیت ممکن نیست");
        foreach (var request in _requests)
        {
            var code = codes[request.Id];
            request.Issue(code.Hash, code.Ciphertext, now);
        }
        Status = PoolPurchaseStatus.Paid; Version++;
    }

    public void ExpireVouchers(DateTimeOffset now)
    {
        if (Status != PoolPurchaseStatus.Paid) return;
        var changed = false;
        foreach (var request in _requests) changed |= request.Expire(now);
        if (changed) Version++;
    }

    public void BeginCancellation()
    {
        if (_requests.Any(x => x.VoucherStatus == PoolVoucherStatus.Redeemed))
            throw new InvalidOperationException("سفارش دارای ووچر مصرف‌شده قابل لغو نیست");
        if (Status is PoolPurchaseStatus.Cancelled or PoolPurchaseStatus.Refunded or PoolPurchaseStatus.CancellationPending) return;
        foreach (var request in _requests) request.Revoke();
        Status = PoolPurchaseStatus.CancellationPending; Version++;
    }

    public void CompleteCancellation(bool refunded)
    {
        if (Status is PoolPurchaseStatus.Cancelled or PoolPurchaseStatus.Refunded) return;
        if (Status != PoolPurchaseStatus.CancellationPending) throw new InvalidOperationException("لغو خرید آماده نیست");
        Status = refunded ? PoolPurchaseStatus.Refunded : PoolPurchaseStatus.Cancelled; Version++;
    }

    public void Redeem(Guid requestId, Guid operatorId, string key, string fingerprint, DateTimeOffset now)
    {
        if (Status != PoolPurchaseStatus.Paid) throw new InvalidOperationException("ووچر قابل مصرف نیست");
        var request = _requests.Single(x => x.Id == requestId);
        request.Redeem(operatorId, key, fingerprint, now); Version++;
    }
}

public sealed class PoolRequest
{
    private PoolRequest() { }
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid PoolId { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string ExternalPoolId { get; private set; } = "";
    public string ExternalRowId { get; private set; } = "";
    public string SnapshotJson { get; private set; } = "";
    public string PoolName { get; private set; } = "";
    public string Date { get; private set; } = "";
    public string Time { get; private set; } = "";
    public int AdultCount { get; private set; }
    public int ChildCount { get; private set; }
    public long TotalMinor { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public PoolReservationStatus ReservationStatus { get; private set; }
    public PoolVoucherStatus? VoucherStatus { get; private set; }
    public string? CodeHash { get; private set; }
    public string? CodeCiphertext { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public DateTimeOffset? RedeemedAtUtc { get; private set; }
    public Guid? RedeemedBy { get; private set; }
    public string? RedemptionKey { get; private set; }
    public string? RedemptionFingerprint { get; private set; }

    public static PoolRequest Create(Guid sessionId, Guid poolId, string provider, string externalPool, string externalRow,
        string snapshot, string poolName, string date, string time, int adults, int children, long total,
        DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (sessionId == Guid.Empty || poolId == Guid.Empty || adults < 0 || children < 0 || (long)adults + children is < 1 or > 100
            || total <= 0 || endsAt <= startsAt) throw new InvalidOperationException("درخواست خرید معتبر نیست");
        return new PoolRequest { Id = Guid.NewGuid(), SessionId = sessionId, PoolId = poolId, ProviderKey = provider,
            ExternalPoolId = externalPool, ExternalRowId = externalRow, SnapshotJson = snapshot, PoolName = poolName,
            Date = date, Time = time, AdultCount = adults, ChildCount = children, TotalMinor = total,
            StartsAtUtc = startsAt, EndsAtUtc = endsAt, ReservationStatus = PoolReservationStatus.ExternalReservationBypassed };
    }

    internal void AssignTo(Guid purchaseId) => PurchaseId = purchaseId;

    internal void Issue(string hash, string ciphertext, DateTimeOffset now)
    {
        if (VoucherStatus.HasValue || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(ciphertext))
            throw new InvalidOperationException("اطلاعات صدور ووچر معتبر نیست");
        CodeHash = hash; CodeCiphertext = ciphertext; IssuedAtUtc = now; VoucherStatus = PoolVoucherStatus.Issued;
    }
    internal bool Expire(DateTimeOffset now)
    {
        if (VoucherStatus != PoolVoucherStatus.Issued || now < EndsAtUtc) return false;
        VoucherStatus = PoolVoucherStatus.Expired; return true;
    }
    internal void Revoke() { if (VoucherStatus is PoolVoucherStatus.Issued or PoolVoucherStatus.Expired) VoucherStatus = PoolVoucherStatus.Revoked; }
    public bool IsRedeemable(DateTimeOffset now) => VoucherStatus == PoolVoucherStatus.Issued && StartsAtUtc <= now && now < EndsAtUtc;
    internal void Redeem(Guid operatorId, string key, string fingerprint, DateTimeOffset now)
    {
        if (!IsRedeemable(now)) throw new InvalidOperationException("ووچر در این زمان قابل مصرف نیست یا قبلاً مصرف شده است");
        VoucherStatus = PoolVoucherStatus.Redeemed; RedeemedBy = operatorId; RedeemedAtUtc = now;
        RedemptionKey = key; RedemptionFingerprint = fingerprint;
    }
}
