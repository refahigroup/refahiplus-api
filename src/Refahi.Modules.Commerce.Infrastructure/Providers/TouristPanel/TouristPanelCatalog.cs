using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed class TouristPanelCatalog(CommerceDbContext db, TouristPanelClient client, IOptions<TouristPanelOptions> options)
{
    public async Task<IReadOnlyList<TpProgramListDto>> ReadAsync(CancellationToken ct)
    {
        var snapshot = await db.CatalogSnapshots
                               .AsNoTracking()
                               .SingleOrDefaultAsync(x => x.AccountKey == options.Value.AccountKey, ct);

        if (snapshot is null) 
            throw new InvalidOperationException("کاتالوگ توریست‌پنل هنوز آماده نیست");

        return JsonSerializer.Deserialize<List<TpProgramListDto>>(snapshot.PayloadJson, TouristPanelClient.Json) ?? [];
    }
    public async Task RefreshAsync(CancellationToken ct)
    {
        var existing = await db.CatalogSnapshots
                               .SingleOrDefaultAsync(x => x.AccountKey == options.Value.AccountKey, ct);

        if (existing?.PublishedAt > DateTimeOffset.UtcNow.AddMinutes(-options.Value.CatalogRefreshMinutes)) 
            return;

        var rows = new List<TpProgramListDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string>? previousPageIds = null;

        for (var page = 1; page <= options.Value.CatalogMaxPages; page++)
        {
            var batch = await client.GetProgramsAsync(page, options.Value.CatalogPageSize, null, null, ct);

            if (batch.Count == 0)
            {
                await PublishAsync(existing, rows, ct);
                return;
            }

            var pageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in batch)
            {
                if (
                    !Guid.TryParse(row.Id, out var id) || 
                    id == Guid.Empty || 
                    !Guid.TryParse(row.SupplyChainHojreId, out var seller) || 
                    seller == Guid.Empty || !pageIds.Add(id.ToString())
                )
                {
                    throw new InvalidOperationException("صفحه تکراری یا شناسه نامعتبر در کاتالوگ توریست‌پنل");
                }
            }

            // TouristPanel currently ignores page/take and repeats the complete result set.
            // A fully repeated consecutive page is therefore its end-of-catalog signal.
            if (previousPageIds is not null && pageIds.SetEquals(previousPageIds))
            {
                await PublishAsync(existing, rows, ct);
                return;
            }

            if (pageIds.Overlaps(seen))
                throw new InvalidOperationException("هم‌پوشانی نامعتبر صفحات کاتالوگ توریست‌پنل");

            rows.AddRange(batch);
            seen.UnionWith(pageIds);
            previousPageIds = pageIds;
        }

        throw new InvalidOperationException("همگام‌سازی کاتالوگ از سقف صفحات عبور کرد");
    }

    private async Task PublishAsync(CommerceCatalogSnapshot? existing, IReadOnlyList<TpProgramListDto> rows, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(rows, TouristPanelClient.Json);

        if (existing is null)
            db.CatalogSnapshots.Add(CommerceCatalogSnapshot.Create(options.Value.AccountKey, json));
        else
            existing.Publish(json);

        await db.SaveChangesAsync(ct);
    }
}

public sealed class TouristPanelCatalogWorker(IServiceScopeFactory scopes, ILogger<TouristPanelCatalogWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var gate = scope.ServiceProvider
                                .GetRequiredService<Application.Contracts.Providers.ICommerceMutationLock>();

                await using var held = await gate.AcquireAsync(new Guid("fd6990d1-ad6f-4cd2-b046-ff4310e69442"), stoppingToken);

                await scope.ServiceProvider
                           .GetRequiredService<TouristPanelCatalog>()
                           .RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) 
            { 
                break; 
            }
            catch (Exception ex) 
            { 
                logger.LogError("TouristPanel catalog refresh failed: {ErrorType}", ex.GetType().Name); 
            }

        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
