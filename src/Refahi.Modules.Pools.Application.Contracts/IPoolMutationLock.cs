namespace Refahi.Modules.Pools.Application.Contracts;

public interface IPoolMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct);
}
