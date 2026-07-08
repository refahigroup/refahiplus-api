using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeCapabilityTests
{
    [Fact]
    public void Irancell_exposes_all_charge_services()
    {
        var capabilities = ChargeCapabilityPolicy.For(ChargeOperator.Irancell);
        Assert.Equal(5, capabilities.Count);
        Assert.All(capabilities, item => Assert.True(item.IsSupported));
    }

    [Fact]
    public void Unsupported_services_include_a_user_safe_reason()
    {
        var capabilities = ChargeCapabilityPolicy.For(ChargeOperator.Mci);
        Assert.False(capabilities.Single(x => x.ServiceType == ChargeServiceType.PostpaidBill).IsSupported);
        Assert.False(capabilities.Single(x => x.ServiceType == ChargeServiceType.CreditLimit).IsSupported);
        Assert.All(capabilities.Where(x => !x.IsSupported), item => Assert.False(string.IsNullOrWhiteSpace(item.UnavailableReason)));
    }

    [Fact]
    public void Cancelling_created_request_is_idempotent()
    {
        var now = DateTime.UtcNow;
        var request = ChargeRequest.Create(Guid.NewGuid(), "Eniac", ChargeOperator.Irancell,
            ChargeServiceType.DirectCharge, "09350000000", null, "CUSTOM", "شارژ مستقیم",
            1001, 0, null, 1, "{}", 50_000, null, 0, 0, 0, 50_000,
            Guid.NewGuid().ToString("N"), now, now.AddMinutes(20));

        request.Cancel(now);
        request.Cancel(now.AddSeconds(1));

        Assert.Equal(ChargeRequestStatus.Cancelled, request.Status);
    }
}
