using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Domain.Aggregates;

public sealed class ChargeMarkupRule
{
    private ChargeMarkupRule() { }
    public Guid Id { get; private set; }
    public ChargeOperator? Operator { get; private set; }
    public ChargeServiceType? ServiceType { get; private set; }
    public decimal Percent { get; private set; }
    public long FixedAmountMinor { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public static ChargeMarkupRule Create(ChargeOperator? @operator, ChargeServiceType? serviceType, decimal percent,
        long fixedAmountMinor, DateTime effectiveFrom, DateTime? effectiveTo, DateTime nowUtc)
    {
        Validate(percent, fixedAmountMinor, effectiveFrom, effectiveTo);

        return new() 
        { 
            Id = Guid.NewGuid(), 
            Operator = @operator, 
            ServiceType = serviceType, 
            Percent = percent,
            FixedAmountMinor = fixedAmountMinor, 
            EffectiveFrom = effectiveFrom, 
            EffectiveTo = effectiveTo,
            IsActive = true, 
            CreatedAt = nowUtc, 
            UpdatedAt = nowUtc 
        };
    }

    public void Update(ChargeOperator? @operator, ChargeServiceType? serviceType, decimal percent, long fixedAmountMinor, DateTime effectiveFrom, DateTime? effectiveTo, DateTime nowUtc)
    {
        Validate(percent, fixedAmountMinor, effectiveFrom, effectiveTo);

        Operator = @operator; 
        ServiceType = serviceType;
        Percent = percent; 
        FixedAmountMinor = fixedAmountMinor; 
        EffectiveFrom = effectiveFrom; 
        EffectiveTo = effectiveTo; 
        UpdatedAt = nowUtc;
    }

    public void Deactivate(DateTime nowUtc) 
    { 
        IsActive = false; 
        UpdatedAt = nowUtc; 
    }

    private static void Validate(decimal percent, long fixedAmountMinor, DateTime from, DateTime? to)
    {
        if (percent is < 0 or > 100) 
            throw new InvalidOperationException("درصد افزایش قیمت باید بین صفر تا صد باشد");

        if (fixedAmountMinor < 0) 
            throw new InvalidOperationException("مبلغ ثابت نمی‌تواند منفی باشد");

        if (to.HasValue && to <= from) 
            throw new InvalidOperationException("بازه اعتبار قانون معتبر نیست");
    }
}
