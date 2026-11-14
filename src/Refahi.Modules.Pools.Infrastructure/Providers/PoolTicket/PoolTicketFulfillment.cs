using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;

public sealed class PoolTicketFulfillment(PoolsDbContext db, IPoolProviderFactory providers, IPoolCodeProtector protector,
    IOptions<PoolTicketOptions> options, TimeProvider clock, ILogger<PoolTicketFulfillment> logger) : IPoolFulfillment
{
    public async Task<PoolFulfillmentResult> ReserveAsync(Guid requestId, PoolSelection selection, CancellationToken ct, long? expectedMinor = null)
    {
        if (selection.ProviderKey != "poolticket") throw new PoolException("تأمین‌کننده رزرو پشتیبانی نمی‌شود", 503);
        var operation = await db.PoolTicketReservations.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        if (operation != null)
        {
            if (operation.Status == PoolTicketOperationStatus.Unknown && operation.ReservationId != null
                && operation.ErrorCode == "POOLS_PROVIDER_RESERVATION_MISMATCH" && operation.Mobile == options.Value.UserMobile)
            {
                try
                {
                    var info = await ((IPoolOrganizationProvider)providers.Get(selection.ProviderKey)).ReservationInfoAsync(operation.ReservationId, ct);
                    var request = await db.Requests.SingleAsync(x => x.Id == requestId, ct);
                    if (info.Id == operation.ReservationId && info.Voucher == protector.Unprotect(operation.VoucherCiphertext!)
                        && info.AdultCount == selection.AdultCount && info.ChildCount == selection.ChildCount
                        && expectedMinor.HasValue && PoolTicketProvider.ToMinor(info.UserPaid, options.Value.PriceUnit) == expectedMinor
                        && info.Date == request.Date && info.Time == request.Time)
                    {
                        operation.Reserve(info.Id, operation.VoucherCiphertext!, info.Enter, clock.GetUtcNow());
                        await db.SaveChangesAsync(ct);
                        logger.LogInformation("PoolTicket reservation verified by known ID. RequestId={RequestId} ReservationId={ReservationId}", requestId, info.Id);
                        return new(PoolFulfillmentOutcome.Reserved, info.Id, info.Voucher);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogWarning("PoolTicket reservation verification pending. RequestId={RequestId} ErrorType={ErrorType}", requestId, ex.GetType().Name); }
            }
            return operation.Status switch
            {
                PoolTicketOperationStatus.Reserved => new(PoolFulfillmentOutcome.Reserved, operation.ReservationId,
                    protector.Unprotect(operation.VoucherCiphertext!)),
                PoolTicketOperationStatus.Rejected => new(PoolFulfillmentOutcome.Rejected),
                _ => new(PoolFulfillmentOutcome.Unknown)
            };
        }
        operation = PoolTicketReservation.Begin(requestId, options.Value.UserMobile ?? "", clock.GetUtcNow());
        db.Add(operation);
        // A committed Sending record forbids re-sending after a crash or an uncertain response.
        await db.SaveChangesAsync(ct);
        try
        {
            var result = await ((IPoolOrganizationProvider)providers.Get(selection.ProviderKey)).ReserveAsync(selection, ct);
            operation.Reserve(result.Id, protector.Protect(result.Voucher), result.Enter, clock.GetUtcNow());
            if (result.AdultCount != selection.AdultCount || result.ChildCount != selection.ChildCount
                || expectedMinor.HasValue && PoolTicketProvider.ToMinor(result.UserPaid, options.Value.PriceUnit) != expectedMinor)
            {
                operation.Fail(false, "POOLS_PROVIDER_RESERVATION_MISMATCH", clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                logger.LogWarning("PoolTicket reservation requires review. RequestId={RequestId} ReservationId={ReservationId} ErrorCode={ErrorCode}",
                    requestId, result.Id, operation.ErrorCode);
                logger.LogWarning("PoolTicket reservation values differ. RequestId={RequestId} ExpectedMinor={ExpectedMinor} ActualPaid={ActualPaid} ExpectedAdults={ExpectedAdults} ActualAdults={ActualAdults} ExpectedChildren={ExpectedChildren} ActualChildren={ActualChildren}",
                    requestId, expectedMinor, result.UserPaid, selection.AdultCount, result.AdultCount, selection.ChildCount, result.ChildCount);
                return new(PoolFulfillmentOutcome.Unknown);
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PoolTicket reservation completed. RequestId={RequestId} ReservationId={ReservationId}", requestId, result.Id);
            return new(PoolFulfillmentOutcome.Reserved, result.Id, result.Voucher);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var code = (ex as PoolException)?.Code ?? "POOLS_PROVIDER_UNKNOWN";
            var definite = code is "POOLS_PROVIDER_RESERVATION_REJECTED" or "POOLS_PROVIDER_CREDIT_LOW"
                or "POOLS_PROVIDER_SELECTION_INVALID" or "POOLS_PROVIDER_TOKEN_INVALID" or "POOLS_PROVIDER_CLIENT_INVALID"
                or "POOLS_PROVIDER_USER_NOT_FOUND" or "POOLS_PROVIDER_USER_INVALID" or "POOLS_PROVIDER_CAPACITY";
            operation.Fail(definite, code, clock.GetUtcNow()); await db.SaveChangesAsync(ct);
            logger.LogWarning("PoolTicket reservation unresolved. RequestId={RequestId} Outcome={Outcome} ErrorCode={ErrorCode}", requestId, operation.Status, code);
            return new(definite ? PoolFulfillmentOutcome.Rejected : PoolFulfillmentOutcome.Unknown);
        }
    }
}
