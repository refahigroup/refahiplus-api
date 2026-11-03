namespace Refahi.Modules.Pools.Application.Contracts;
public enum PoolFulfillmentOutcome { Reserved, Rejected, Unknown }
public sealed record PoolFulfillmentResult(PoolFulfillmentOutcome Outcome, string? ReservationId = null, string? Voucher = null);
public interface IPoolFulfillment
{
    Task<PoolFulfillmentResult> ReserveAsync(Guid requestId, PoolSelection selection, CancellationToken ct, long? expectedMinor = null);
}
