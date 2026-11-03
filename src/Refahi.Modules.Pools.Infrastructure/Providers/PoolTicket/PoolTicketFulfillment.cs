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
            return operation.Status switch
            {
                PoolTicketOperationStatus.Reserved => new(PoolFulfillmentOutcome.Reserved, operation.ReservationId,
                    protector.Unprotect(operation.VoucherCiphertext!)),
                PoolTicketOperationStatus.Rejected => new(PoolFulfillmentOutcome.Rejected),
                _ => new(PoolFulfillmentOutcome.Unknown)
            };
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
