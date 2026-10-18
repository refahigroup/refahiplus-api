namespace Refahi.Modules.Promotions.Domain;

public sealed class PromotionCode
{
    private PromotionCode() { }
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string Hash { get; private set; } = "";
    public string Ciphertext { get; private set; } = "";
    public string Mask { get; private set; } = "";
    public Guid? UserId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int MaximumUses { get; private set; }
    public string BatchKey { get; private set; } = "";
    public static PromotionCode Create(Guid campaignId, string hash, string ciphertext, string mask,
        Guid? userId, int maximumUses, string batchKey) => new()
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Hash = hash,
            Ciphertext = ciphertext,
            Mask = mask,
            UserId = userId,
            MaximumUses = maximumUses,
            BatchKey = batchKey
        };
    public void Disable() => IsActive = false;
}

public sealed class PromotionRedemption
{
    private PromotionRedemption() { }
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid CodeId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public long DiscountMinor { get; private set; }
    public long RuleVersion { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public RedemptionStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? PaymentId { get; private set; }
    private readonly List<PromotionRedemptionItem> _items = [];
    public IReadOnlyCollection<PromotionRedemptionItem> Items => _items;
    public bool OccupiesCapacity => Status is RedemptionStatus.Reserved or RedemptionStatus.PaymentPending or RedemptionStatus.Committed;
    public static PromotionRedemption Reserve(Guid campaignId, Guid codeId, Guid orderId, Guid userId,
        long version, string key, string fingerprint, PromotionCalculation calculation, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var value = new PromotionRedemption
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            CodeId = codeId,
            OrderId = orderId,
            UserId = userId,
            RuleVersion = version,
            IdempotencyKey = key,
            Fingerprint = fingerprint,
            DiscountMinor = calculation.DiscountMinor,
            OrganizationId = calculation.OrganizationId,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };
        value._items.AddRange(calculation.Shares.Select(x => PromotionRedemptionItem.Create(value.Id, x)));
        return value;
    }
    public void BeginPayment(DateTimeOffset now)
    {
        if (Status == RedemptionStatus.PaymentPending) return;
        if (Status != RedemptionStatus.Reserved || ExpiresAt <= now)
            throw new PromotionRuleException("رزرو تخفیف منقضی شده یا قابل پرداخت نیست");
        Status = RedemptionStatus.PaymentPending;
    }
    public void Commit(Guid paymentId)
    {
        if (Status == RedemptionStatus.Committed && PaymentId != paymentId) throw new PromotionRuleException("شناسه پرداخت با مصرف ثبت‌شده مطابقت ندارد");
        if (Status is RedemptionStatus.Reversed or RedemptionStatus.Committed) return;
        if (Status != RedemptionStatus.PaymentPending) throw new PromotionRuleException("رزرو تخفیف برای ثبت پرداخت معتبر نیست");
        PaymentId = paymentId; Status = RedemptionStatus.Committed;
    }
    public void Release(bool expired = false)
    {
        if (Status is RedemptionStatus.Committed or RedemptionStatus.Reversed)
            throw new PromotionRuleException("مصرف پرداخت‌شده تنها پس از بازگشت وجه آزاد می‌شود");
        Status = expired ? RedemptionStatus.Expired : RedemptionStatus.Released;
    }
    public void Reverse() => Status = RedemptionStatus.Reversed;
}

public sealed class PromotionRedemptionItem
{
    private PromotionRedemptionItem() { }
    public Guid Id { get; private set; }
    public Guid RedemptionId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public long AmountMinor { get; private set; }
    internal static PromotionRedemptionItem Create(Guid redemptionId, PromotionShare share) => new()
    { Id = Guid.NewGuid(), RedemptionId = redemptionId, OrderItemId = share.OrderItemId, AmountMinor = share.AmountMinor };
}

public sealed class PromotionAudit
{
    private PromotionAudit() { }
    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Action { get; private set; } = "";
    public string Details { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public static PromotionAudit Create(Guid actorId, Guid subjectId, string action, DateTimeOffset now, string details = "") =>
        new() { Id = Guid.NewGuid(), ActorId = actorId, SubjectId = subjectId, Action = action, OccurredAt = now, Details = details };
}
