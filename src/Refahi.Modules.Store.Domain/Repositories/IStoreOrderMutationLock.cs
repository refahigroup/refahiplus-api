namespace Refahi.Modules.Store.Domain.Repositories;

public interface IStoreOrderMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid orderId, CancellationToken ct);
}
