using System.Numerics;
namespace Refahi.Modules.Promotions.Domain;

public enum CampaignStatus { Draft, Active, Paused }
public enum DiscountKind { Fixed, Percentage }
public enum RedemptionStatus { Reserved, PaymentPending, Committed, Released, Expired, Reversed }

public sealed class PromotionRuleException(string message) : Exception(message);

public sealed class PromotionCampaign
{
    private PromotionCampaign() { }
    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public CampaignStatus Status { get; private set; }
    public DiscountKind Kind { get; private set; }
    public long FixedAmountMinor { get; private set; }
    public decimal Percent { get; private set; }
    public long MaximumDiscountMinor { get; private set; }
    public long MinimumEligibleMinor { get; private set; }
    public long BudgetMinor { get; private set; }
    public int MaximumUses { get; private set; }
    public int MaximumUsesPerUser { get; private set; }
    public string[] SourceModules { get; private set; } = [];
    public string[] CategoryPrefixes { get; private set; } = [];
    public Guid[] OrganizationIds { get; private set; } = [];
    public Guid[] UserIds { get; private set; } = [];
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public long Version { get; private set; } = 1;

    public static PromotionCampaign Create(string title, DiscountKind kind, long fixedAmountMinor,
        decimal percent, long maximumDiscountMinor, long minimumEligibleMinor, long budgetMinor,
        int maximumUses, int maximumUsesPerUser, string[] modules, string[] prefixes,
        Guid[] organizations, Guid[] users, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        var campaign = new PromotionCampaign { Id = Guid.NewGuid() };
        campaign.Update(title, kind, fixedAmountMinor, percent, maximumDiscountMinor,
            minimumEligibleMinor, budgetMinor, maximumUses, maximumUsesPerUser,
            modules, prefixes, organizations, users, startsAt, endsAt);
        return campaign;
    }

    public void Update(string title, DiscountKind kind, long fixedAmountMinor, decimal percent,
        long maximumDiscountMinor, long minimumEligibleMinor, long budgetMinor, int maximumUses,
        int maximumUsesPerUser, string[] modules, string[] prefixes, Guid[] organizations,
        Guid[] users, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (Status != CampaignStatus.Draft) throw new PromotionRuleException("قواعد کمپین فعال قابل ویرایش نیست");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || !Enum.IsDefined(kind)
            || maximumDiscountMinor <= 0 || minimumEligibleMinor < 0 || budgetMinor <= 0
            || maximumUses <= 0 || maximumUsesPerUser <= 0 || endsAt <= startsAt
            || (kind == DiscountKind.Fixed && fixedAmountMinor <= 0)
            || (kind == DiscountKind.Percentage && (percent <= 0 || percent > 100)))
            throw new PromotionRuleException("قواعد کمپین معتبر نیست");
        Title = title.Trim(); Kind = kind; FixedAmountMinor = fixedAmountMinor; Percent = percent;
        MaximumDiscountMinor = maximumDiscountMinor; MinimumEligibleMinor = minimumEligibleMinor;
        BudgetMinor = budgetMinor; MaximumUses = maximumUses; MaximumUsesPerUser = maximumUsesPerUser;
        SourceModules = modules.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        CategoryPrefixes = prefixes.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (SourceModules.Any(string.IsNullOrWhiteSpace) || CategoryPrefixes.Any(string.IsNullOrWhiteSpace)
            || organizations.Contains(Guid.Empty) || users.Contains(Guid.Empty))
            throw new PromotionRuleException("محدودیت‌های کمپین معتبر نیست");
        OrganizationIds = organizations.Distinct().ToArray(); UserIds = users.Distinct().ToArray();
        StartsAt = startsAt.ToUniversalTime(); EndsAt = endsAt.ToUniversalTime(); Version++;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        if (active && EndsAt <= now) throw new PromotionRuleException("کمپین منقضی شده است");
        Status = active ? CampaignStatus.Active : CampaignStatus.Paused;
    }

    public PromotionCalculation Calculate(Guid userId, string module, IReadOnlyCollection<Guid> organizations,
        IReadOnlyList<PromotionLine> lines, long payableMinor, DateTimeOffset now)
    {
        if (Status != CampaignStatus.Active || now < StartsAt || now >= EndsAt
            || (UserIds.Length > 0 && !UserIds.Contains(userId))
            || (SourceModules.Length > 0 && !SourceModules.Contains(module, StringComparer.OrdinalIgnoreCase)))
            throw new PromotionRuleException("کد تخفیف برای این سفارش معتبر نیست");
        var organization = OrganizationIds.Intersect(organizations).Order().Cast<Guid?>().FirstOrDefault();
        if (OrganizationIds.Length > 0 && !organization.HasValue)
            throw new PromotionRuleException("کد تخفیف برای این سفارش معتبر نیست");
        if (lines.Count == 0 || lines.Any(x => x.AmountMinor <= 0) || lines.Select(x => x.Id).Distinct().Count() != lines.Count)
            throw new PromotionRuleException("اقلام سفارش معتبر نیست");
        var eligible = lines.Where(x => CategoryPrefixes.Length == 0 || CategoryPrefixes.Any(p =>
            x.CategoryCode.StartsWith(p, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Id).ToArray();
        var basis = eligible.Aggregate(0L, (s, x) => checked(s + x.AmountMinor));
        if (basis <= 0 || basis < MinimumEligibleMinor) throw new PromotionRuleException("حداقل مبلغ اقلام مشمول تخفیف تأمین نشده است");
        var discount = Kind == DiscountKind.Fixed ? FixedAmountMinor
            : checked((long)Math.Round((decimal)basis * Percent / 100m, 0, MidpointRounding.AwayFromZero));
        discount = Math.Min(discount, MaximumDiscountMinor);
        if (discount <= 0 || discount > basis || discount >= payableMinor)
            throw new PromotionRuleException("مبلغ تخفیف باید مثبت باشد و مبلغ پرداخت مشتری را صفر نکند");
        var shares = eligible.Select(x =>
        {
            var amount = BigInteger.DivRem((BigInteger)discount * x.AmountMinor, basis, out var remainder);
            return new { x.Id, Amount = (long)amount, Remainder = remainder };
        }).ToArray();
        var amounts = shares.ToDictionary(x => x.Id, x => x.Amount);
        var remaining = discount - amounts.Values.Sum();
        foreach (var share in shares.OrderByDescending(x => x.Remainder).ThenBy(x => x.Id).Take((int)remaining)) amounts[share.Id]++;
        return new(discount, organization, amounts.Where(x => x.Value > 0).Select(x => new PromotionShare(x.Key, x.Value)).ToArray());
    }
}

public sealed record PromotionLine(Guid Id, string CategoryCode, long AmountMinor);
public sealed record PromotionShare(Guid OrderItemId, long AmountMinor);
public sealed record PromotionCalculation(long DiscountMinor, Guid? OrganizationId, PromotionShare[] Shares);
