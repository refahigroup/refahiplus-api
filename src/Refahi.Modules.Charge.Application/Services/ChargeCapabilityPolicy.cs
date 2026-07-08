using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Application.Services;

public static class ChargeCapabilityPolicy
{
    public static IReadOnlyList<ChargeServiceCapabilityDto> For(ChargeOperator @operator)
        => Enum.GetValues<ChargeServiceType>().Select(service => Get(@operator, service)).ToArray();

    public static ChargeServiceCapabilityDto Get(ChargeOperator @operator, ChargeServiceType service)
    {
        var supported = service switch
        {
            ChargeServiceType.PostpaidBill or ChargeServiceType.CreditLimit => @operator == ChargeOperator.Irancell,
            ChargeServiceType.PinCharge => @operator is ChargeOperator.Irancell or ChargeOperator.Mci or ChargeOperator.Rightel,
            _ => true
        };
        var reason = supported ? null : service switch
        {
            ChargeServiceType.PostpaidBill => "پرداخت قبض دائمی برای این اپراتور فعال نیست",
            ChargeServiceType.CreditLimit => "افزایش حد اعتبار برای این اپراتور فعال نیست",
            ChargeServiceType.PinCharge => "پین شارژ برای این اپراتور ارائه نمی‌شود",
            _ => "این خدمت برای اپراتور انتخابی فعال نیست"
        };
        var minimum = service switch
        {
            ChargeServiceType.DirectCharge when @operator == ChargeOperator.Irancell => 5_000,
            ChargeServiceType.DirectCharge when @operator == ChargeOperator.Mci => 50_000,
            ChargeServiceType.DirectCharge => 1_000,
            ChargeServiceType.CreditLimit => 10_000,
            _ => (long?)null
        };
        long? maximum = service is ChargeServiceType.DirectCharge or ChargeServiceType.CreditLimit ? 10_000_000 : null;
        IReadOnlyList<long> suggestions = service is ChargeServiceType.DirectCharge or ChargeServiceType.CreditLimit
            ? [50_000, 100_000, 200_000, 500_000] : [];
        return new(service, supported, reason, minimum, maximum, suggestions);
    }
}
