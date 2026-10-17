using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
public sealed class PoolTicketReservationGateway : IPoolReservationGateway
{
    public Task<bool> ReserveAsync(Guid requestId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // shortcut: explicitly approved reservation bypass; replace with the real provider reservation before claiming external capacity is held.
        return Task.FromResult(true);
    }
}
