using Microsoft.AspNetCore.Http;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Api.Security;

public sealed class InPersonTypedErrorFilter : IEndpointFilter
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
        catch (StoreDomainException ex)
        {
            var status =
                ex.ErrorCode.EndsWith("_NOT_FOUND", StringComparison.Ordinal)
                || ex.ErrorCode is "SHOP_NOT_FOUND" or "PRODUCT_NOT_FOUND" or "ORDER_NOT_FOUND"
                    ? StatusCodes.Status404NotFound
                : ex.ErrorCode.Contains("OTP", StringComparison.Ordinal)
                || ex.ErrorCode.Contains("PAYMENT", StringComparison.Ordinal)
                || ex.ErrorCode.Contains("IDEMPOTENCY", StringComparison.Ordinal)
                || ex.ErrorCode == "AGREEMENT_TERM_NOT_EFFECTIVE"
                    ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Json(
                new InPersonErrorResponse(false, ex.ErrorCode, ex.Message, status),
                statusCode: status
            );
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(
                new InPersonErrorResponse(
                    false,
                    "IN_PERSON_FORBIDDEN",
                    ex.Message,
                    StatusCodes.Status403Forbidden
                ),
                statusCode: StatusCodes.Status403Forbidden
            );
        }
    }
}

public sealed record InPersonErrorResponse(
    bool Success,
    string Code,
    string Message,
    int StatusCode
);
