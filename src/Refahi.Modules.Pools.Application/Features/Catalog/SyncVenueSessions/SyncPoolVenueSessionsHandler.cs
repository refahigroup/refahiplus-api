using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Catalog.SyncVenueSessions;

public sealed record SyncPoolVenueSessionsCommand(Guid Id) : IRequest;
public sealed class SyncPoolVenueSessionsHandler(IPoolProviderFactory providers, IPoolCatalogRepository repository,
    IPoolMutationLock gate, IPoolSessionTimeParser times, TimeProvider clock) : IRequestHandler<SyncPoolVenueSessionsCommand>
{
    public async Task<Unit> Handle(SyncPoolVenueSessionsCommand request, CancellationToken ct)
    {
        await using var lease = await gate.AcquireAsync(request.Id, ct);
        var pool = await repository.PoolAsync(request.Id, ct); if (pool?.IsActive != true) return Unit.Value;
        var provider = providers.Get(pool.ProviderKey);
        var groups = await provider.SessionsAsync(pool.ExternalId, ct);
        var terms = await provider.TermsAsync(pool.ExternalId, ct);
        var existing = (await repository.PoolSessionsAsync(pool.Id, ct)).ToDictionary(x => (x.ExternalGroupId, x.ExternalRowId));
        var observed = new HashSet<(string, string)>();
        foreach (var group in groups) foreach (var row in group.Rows)
        {
            var key = (group.Id, row.Id); if (!observed.Add(key)) throw new PoolException("فهرست کامل و یکتای تأمین‌کننده دریافت نشد", 502);
            if (!existing.TryGetValue(key, out var entry)) { entry = PoolSessionEntry.Create(pool.Id, group.Id, row.Id); await repository.AddSessionAsync(entry, ct); }
            var range = times.Parse(row.Date, row.Time);
            entry.Refresh(group.Name, group.Gender, row.Date, row.Time, range.Start, range.End,
                PoolKeys.Minor(row.AdultPrice, row.PriceUnit), PoolKeys.Minor(row.ChildPrice, row.PriceUnit),
                row.AdultEnabled, row.ChildEnabled, row.IsSelling, row.DescriptionHtml, row.TermsHtml, clock.GetUtcNow());
        }
        foreach (var old in existing.Values.Where(x => !observed.Contains((x.ExternalGroupId, x.ExternalRowId)))) old.Deactivate();
        pool.RefreshSessions(terms, clock.GetUtcNow()); await repository.SaveAsync(ct);
        return Unit.Value;
    }
}
