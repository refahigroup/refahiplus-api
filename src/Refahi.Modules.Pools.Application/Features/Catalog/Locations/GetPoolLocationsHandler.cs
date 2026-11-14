using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.Locations;
public sealed class GetPoolLocationsHandler(IPoolCatalogReader reader) : IRequestHandler<GetPoolLocationsQuery, IReadOnlyList<PoolLocationDto>>
{
    public async Task<IReadOnlyList<PoolLocationDto>> Handle(GetPoolLocationsQuery r, CancellationToken ct)
    { return await reader.LocationsAsync(r.State, r.Cities, ct); }
}
