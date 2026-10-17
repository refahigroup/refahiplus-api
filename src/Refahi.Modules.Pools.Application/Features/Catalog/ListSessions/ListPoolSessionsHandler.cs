using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.ListSessions;
public sealed class ListPoolSessionsHandler(IPoolCatalogReader reader) : IRequestHandler<ListPoolSessionsQuery, PoolPage<PoolSessionDto>>
{
    public async Task<PoolPage<PoolSessionDto>> Handle(ListPoolSessionsQuery r, CancellationToken ct)
    { return await reader.SessionsAsync(r.Filter, ct); }
}
