using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Charge.Application.Contracts.Providers;
namespace Refahi.Modules.Charge.Infrastructure.Providers;

public sealed class ChargeSecretProtector : IChargeSecretProtector
{
    private readonly IDataProtector _protector;
    public ChargeSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Refahi.Charge.Pin.v1");
    }

    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}
