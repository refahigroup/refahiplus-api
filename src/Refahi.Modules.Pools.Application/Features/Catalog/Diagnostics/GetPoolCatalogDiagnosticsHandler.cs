using MediatR;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Catalog.Diagnostics;
public sealed class GetPoolCatalogDiagnosticsHandler(IPoolCatalogDiagnostics diagnostics):IRequestHandler<GetPoolCatalogDiagnosticsQuery,PoolCatalogDiagnosticsDto>
{
    public Task<PoolCatalogDiagnosticsDto> Handle(GetPoolCatalogDiagnosticsQuery r,CancellationToken ct)=>diagnostics.ReadAsync(ct);
}
