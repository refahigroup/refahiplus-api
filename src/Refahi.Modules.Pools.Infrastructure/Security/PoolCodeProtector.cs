using Refahi.Modules.Pools.Application.Contracts;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Infrastructure.Security;
public sealed class PoolCodeProtector(IDataProtectionProvider provider) : IPoolCodeProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Refahi.Pools.VoucherCode.v1");
    public string Protect(string code) => _protector.Protect(code);
    public string Unprotect(string ciphertext) { try { return _protector.Unprotect(ciphertext); } catch (CryptographicException) { throw new PoolException("نمایش ووچر موقتاً ممکن نیست",503,"VOUCHER_UNAVAILABLE"); } }
}
