using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.GetSession;
public sealed class GetPoolSessionHandler(IPoolCatalogReader reader) : IRequestHandler<GetPoolSessionQuery, PoolSessionDto>
{
    public async Task<PoolSessionDto> Handle(GetPoolSessionQuery r, CancellationToken ct)
    { return await reader.SessionAsync(r.Id, ct) ?? throw new PoolException("سانس یافت نشد", 404); }
}
