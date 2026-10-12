using MediatR;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Application.Features.Catalog;

public sealed class PoolCatalogHandlers(IPoolProviderFactory providers, IPoolCatalogStore store) :
    IRequestHandler<PoolProvidersQuery, IReadOnlyList<PoolProviderInfo>>,
    IRequestHandler<PoolStatesQuery, IReadOnlyList<PoolState>>,
    IRequestHandler<PoolCitiesQuery, IReadOnlyList<PoolCity>>,
    IRequestHandler<PoolSearchQuery, PoolCatalog>, IRequestHandler<PoolDetailsQuery, PoolDetails>,
    IRequestHandler<PoolSessionsQuery, IReadOnlyList<PoolSession>>, IRequestHandler<PoolTermsQuery, string>,
    IRequestHandler<PoolQuoteQuery, PoolQuote>
{
    public Task<IReadOnlyList<PoolProviderInfo>> Handle(PoolProvidersQuery r, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PoolProviderInfo>>(providers.Enabled().Select(p => new PoolProviderInfo(p.Key, p.Name, p.Capabilities)).ToArray());
    public Task<IReadOnlyList<PoolState>> Handle(PoolStatesQuery r, CancellationToken ct) => providers.Get(r.ProviderKey).StatesAsync(r.ActiveOnly, ct);
    public Task<IReadOnlyList<PoolCity>> Handle(PoolCitiesQuery r, CancellationToken ct) => providers.Get(r.ProviderKey).CitiesAsync(r.StateId, r.ActiveOnly, ct);
    public async Task<PoolCatalog> Handle(PoolSearchQuery r, CancellationToken ct)
    {
        var result = await providers.Get(r.ProviderKey).SearchAsync(r, ct);
        await store.UpsertAsync(result.Items, ct); return result;
    }
    public async Task<PoolDetails> Handle(PoolDetailsQuery r, CancellationToken ct)
    {
        var p = providers.Get(r.ProviderKey);
        // No details-by-id endpoint exists upstream. The snapshot supplies only previously observed display data.
        var sessions = await p.SessionsAsync(r.PoolId, ct);
        var terms = await p.TermsAsync(r.PoolId, ct);
        return new(await store.GetAsync(p.Key, r.PoolId, ct), sessions, terms, p.Capabilities);
    }
    public Task<IReadOnlyList<PoolSession>> Handle(PoolSessionsQuery r, CancellationToken ct) => providers.Get(r.ProviderKey).SessionsAsync(r.PoolId, ct);
    public Task<string> Handle(PoolTermsQuery r, CancellationToken ct) => providers.Get(r.ProviderKey).TermsAsync(r.PoolId, ct);
    public Task<PoolQuote> Handle(PoolQuoteQuery r, CancellationToken ct) => providers.Get(r.Selection.ProviderKey).QuoteAsync(r.Selection, ct);
}
