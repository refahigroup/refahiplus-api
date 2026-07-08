using Refahi.Modules.Charge.Domain.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
namespace Refahi.Modules.Charge.Api.Endpoints;
internal static class ChargeEndpointHelpers
{
    public static bool TryUserId(HttpContext context, out Guid userId)
        => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out userId);
    public static ChargeOperator ParseOperator(string value) => value.ToLowerInvariant() switch
    {
        "1" or "irancell" => ChargeOperator.Irancell, "2" or "mci" => ChargeOperator.Mci,
        "3" or "rightel" => ChargeOperator.Rightel, "4" or "shatel" => ChargeOperator.Shatel,
        "5" or "taliya" => ChargeOperator.Taliya, _ => throw new ArgumentException("اپراتور معتبر نیست")
    };
}
