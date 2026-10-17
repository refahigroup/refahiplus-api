using System.Security.Cryptography;
using System.Text;
using Refahi.Modules.Pools.Application.Contracts;
namespace Refahi.Modules.Pools.Application;
internal static class PoolKeys
{
    public const string Category = "entertainment.waterpark";
    public const string GrantIssuer = "Pools";
    public static string Order(Guid id) => $"pools-purchase:{id:N}";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static Guid Lock(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value))[..16]);
    public static long Minor(decimal value, string? unit)
    {
        try
        {
            if (value < 0 || unit is not ("TOMAN" or "IRR")) throw new PoolException("مبلغ تأمین‌کننده معتبر نیست", 502);
            var minor = checked(value * (unit == "TOMAN" ? 10 : 1));
            if (minor != decimal.Truncate(minor)) throw new PoolException("مبلغ تأمین‌کننده معتبر نیست", 502);
            return checked((long)minor);
        }
        catch (OverflowException) { throw new PoolException("مبلغ تأمین‌کننده معتبر نیست", 502); }
    }
}
