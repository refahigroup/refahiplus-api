using Microsoft.AspNetCore.Http;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Commerce.Api.Endpoints.Cart;

internal static class _Helpers
{
    public static bool TryUser(HttpContext c, out Guid id) =>
        Guid.TryParse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? c.User.FindFirstValue("sub"), out id);

    public static string Role(HttpContext c)
        => c.User.IsInRole("Admin") ? "Admin" : "User";

    public static IResult Unauthorized()
        => Results.Json(ApiResponseHelper.Error("احراز هویت انجام نشده است", statusCode: 401), statusCode: 401);

    public static IResult PriceConflict(CommercePriceChangedException ex) =>
        Results.Conflict(new
        {
            Success = false,
            Data = ex.Current,
            Message = "قیمت یا ظرفیت تغییر کرده است",
            StatusCode = StatusCodes.Status409Conflict
        });

}
