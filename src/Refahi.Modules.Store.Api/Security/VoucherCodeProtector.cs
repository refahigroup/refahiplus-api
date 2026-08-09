using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Store.Application.Contracts.Vouchers;

namespace Refahi.Modules.Store.Api.Security;

internal sealed class VoucherCodeProtector : IVoucherCodeProtector
{
    private readonly IDataProtector _protector;

    public VoucherCodeProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("Refahi.Store.VoucherCode.v1");

    public string Protect(string plaintextCode) => _protector.Protect(plaintextCode);

    public bool TryUnprotect(string ciphertext, out string plaintextCode)
    {
        plaintextCode = string.Empty;
        if (string.IsNullOrWhiteSpace(ciphertext)) return false;
        try
        {
            plaintextCode = _protector.Unprotect(ciphertext);
            return !string.IsNullOrWhiteSpace(plaintextCode);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
