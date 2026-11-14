using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.GetVenue;
public sealed class GetPoolVenueHandler(IPoolCatalogReader reader) : IRequestHandler<GetPoolVenueQuery, PoolVenueDetailsDto>
{
    public async Task<PoolVenueDetailsDto> Handle(GetPoolVenueQuery r, CancellationToken ct)
    { return await reader.VenueAsync(r.Id, ct) ?? throw new PoolException("استخر یافت نشد", 404); }
}
