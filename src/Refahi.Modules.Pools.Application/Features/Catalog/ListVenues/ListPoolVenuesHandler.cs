using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.ListVenues;
public sealed class ListPoolVenuesHandler(IPoolCatalogReader reader) : IRequestHandler<ListPoolVenuesQuery, PoolPage<PoolVenueDto>>
{
    public async Task<PoolPage<PoolVenueDto>> Handle(ListPoolVenuesQuery r, CancellationToken ct)
    { return await reader.VenuesAsync(r.Filter, ct); }
}
