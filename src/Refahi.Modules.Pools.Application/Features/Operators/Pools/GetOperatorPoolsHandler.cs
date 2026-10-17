using MediatR;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.Pools;
public sealed class GetOperatorPoolsHandler(PoolOperatorAccess operators, IPoolCatalogReader reader) : IRequestHandler<GetOperatorPoolsQuery, IReadOnlyList<PoolVenueDto>>
{
    public async Task<IReadOnlyList<PoolVenueDto>> Handle(GetOperatorPoolsQuery r, CancellationToken ct)
    {
        var result = new List<PoolVenueDto>();
        foreach (var id in (await operators.Grants(r.UserId, ct)).Select(x => PoolOperatorAccess.Parse(x.Value)).Where(x => x.HasValue).Distinct())
        { var p = await reader.VenueAsync(id!.Value, ct); if (p != null) result.Add(p.Pool); }
        return result;
    }
}
