using Microsoft.AspNetCore.Http;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Api.Security;

public sealed class VoucherTypedErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        try
        {
            return await next(context);
        }
        catch (VoucherApplicationException ex)
        {
            var status =
                ex.Code.EndsWith("_FORBIDDEN", StringComparison.Ordinal)
                    ? StatusCodes.Status403Forbidden
                : ex.Code is "VOUCHER_NOT_FOUND" or "STORE_ORDER_NOT_FOUND"
                    ? StatusCodes.Status404NotFound
                : ex.Code.Contains("REDEEM", StringComparison.Ordinal)
                || ex.Code.Contains("IDEMPOTENCY", StringComparison.Ordinal)
                || ex.Code.Contains("CONFLICT", StringComparison.Ordinal)
                    ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new VoucherErrorResponse(false, ex.Code, ex.Message, status),
                statusCode: status
            );
        }
        catch (StoreConcurrencyException)
        {
            const int status = StatusCodes.Status409Conflict;
            return Results.Json(
                new VoucherErrorResponse(
                    false,
                    "VOUCHER_CONCURRENCY_CONFLICT",
                    "وضعیت ووچر هم‌زمان تغییر کرده است؛ دوباره تلاش کنید",
                    status
                ),
                statusCode: status
            );
        }
        catch (StoreDomainException ex)
        {
            const int status = StatusCodes.Status400BadRequest;
            return Results.Json(
                new VoucherErrorResponse(false, ex.ErrorCode, ex.Message, status),
                statusCode: status
            );
        }
    }
}
