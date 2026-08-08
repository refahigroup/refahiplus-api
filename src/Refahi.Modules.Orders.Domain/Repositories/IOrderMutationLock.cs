namespace Refahi.Modules.Orders.Domain.Repositories;

public interface IOrderMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct);
}
