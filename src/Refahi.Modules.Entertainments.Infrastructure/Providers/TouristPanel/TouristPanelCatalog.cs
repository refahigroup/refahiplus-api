using System.Text.RegularExpressions;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;
using Refahi.Modules.Media.Application.Contracts.External;
namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

public sealed class TouristPanelCatalog(EntertainmentDbContext db, TouristPanelClient client,
    IOptions<TouristPanelOptions> options, IOptions<EntertainmentsOptions> moduleOptions, IPersistentExternalMedia media, ILogger<TouristPanelCatalog> logger)
{
    public async Task<IReadOnlyList<TpProgramListDto>> ReadAsync(CancellationToken ct)
    {
        var state = await db.SyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.AccountKey == options.Value.AccountKey, ct);
        if (state?.LastSuccessUtc is null || state.LastSuccessUtc < DateTimeOffset.UtcNow.AddDays(-moduleOptions.Value.MaxDiscoveryAgeDays)) throw new EntertainmentUnavailableException();
        var published = await db.Programs.AsNoTracking().Where(x => x.AccountKey == options.Value.AccountKey && x.IsListed && x.ObservedAtUtc >= DateTimeOffset.UtcNow.AddDays(-moduleOptions.Value.MaxDiscoveryAgeDays) && x.ListedAtUtc >= DateTimeOffset.UtcNow.AddDays(-moduleOptions.Value.MaxDiscoveryAgeDays)).ToListAsync(ct);
        if (published.Count == 0 && await db.Programs.AnyAsync(x => x.AccountKey == options.Value.AccountKey && x.IsListed, ct))
            throw new EntertainmentUnavailableException();
        return published
            .Select(x => new TpProgramListDto { Id = x.ProgramKey.ToString("N"), SupplyChainHojreId = x.SupplierId,
                SupplyChainHojre = new() { Title = x.SupplierTitle }, Title = x.Title, Type = (TpProgramType)x.ProgramType,
                IsActive = x.IsListed, IndexImage = x.Images.FirstOrDefault(),
                ProgramCategory = x.CategoryCode.HasValue ? new() { Code = x.CategoryCode.Value, Title = x.CategoryTitle } : null,
                LocationData = new() { Code = x.LocationCode, Native = x.NativeCityName } }).ToArray();
    }
    public async Task RefreshAsync(CancellationToken ct)
    {
        var o = options.Value; var now = DateTimeOffset.UtcNow;
        var state = await db.SyncStates.SingleOrDefaultAsync(x => x.AccountKey == o.AccountKey, ct);
        if (state is null) { state = TouristPanelSyncState.Create(o.AccountKey); db.SyncStates.Add(state); await db.SaveChangesAsync(ct); }
        if (state.NextAttemptUtc <= now)
        {
            try
            {
                var rows = new List<TpProgramListDto>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string>? previous = null; var complete = false;
                for (var page = 1; page <= o.CatalogMaxPages; page++)
                {
                    var batch = await client.GetProgramsAsync(page, o.CatalogPageSize, null, null, ct);
                    if (batch.Count == 0) { complete = true; break; }
                    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in batch)
                        if (!Guid.TryParse(item.Id, out var id) || id == Guid.Empty || !Guid.TryParse(item.SupplyChainHojreId, out var supplier)
                            || supplier == Guid.Empty || !ids.Add(id.ToString())) throw new InvalidOperationException("شناسه یا صفحه تکراری نامعتبر است");
                    if (previous is not null && ids.SetEquals(previous)) { complete = true; break; }
                    if (ids.Overlaps(seen)) throw new InvalidOperationException("هم‌پوشانی صفحات معتبر نیست");
                    rows.AddRange(batch); seen.UnionWith(ids); previous = ids;
                }
                if (!complete) throw new InvalidOperationException("سقف صفحات دریافت خدمات رعایت نشد");
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var existing = await db.Programs.Where(x => x.AccountKey == o.AccountKey).ToDictionaryAsync(x => x.NativeId, StringComparer.OrdinalIgnoreCase, ct);
                foreach (var row in existing.Values) row.Retire();
                foreach (var item in rows)
                {
                    var native = Guid.Parse(item.Id!).ToString();
                    if (!existing.TryGetValue(native, out var row)) { row = TouristPanelProgram.Create(o.AccountKey, native); db.Programs.Add(row); }
                    row.List(Guid.Parse(item.SupplyChainHojreId!).ToString(), Plain(item.SupplyChainHojre?.Title) ?? "تأمین‌کننده", Plain(item.Title) ?? "خدمت",
                        (int)item.Type, item.IsActive, item.LocationData?.Code, Plain(item.LocationData?.City?.Native ?? item.LocationData?.Native),
                        item.ProgramCategory?.Code, Plain(item.ProgramCategory?.Title), now);
                }
                state.Success(now, o.CatalogRefreshMinutes); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                // A failed list never replaces its last successfully published membership.
                db.ChangeTracker.Clear(); state = await db.SyncStates.SingleAsync(x => x.AccountKey == o.AccountKey, ct);
                state.Failed(now); await db.SaveChangesAsync(ct); throw;
            }
        }
        var due = await db.Programs.Where(x => x.AccountKey == o.AccountKey && x.IsListed && x.NextDetailAttemptUtc <= now).ToListAsync(ct);
        // One account lock serializes publication across instances. External calls never hold a DB transaction.
        foreach (var row in due)
        {
            try
            {
                var detail = await client.GetDetailAsync(row.NativeId, null, ct);
                if (!Guid.TryParse(detail.Id, out var id) || id != Guid.Parse(row.NativeId) ||
                    !Guid.TryParse(detail.SupplyChainHojreId, out var supplier) || supplier != Guid.Parse(row.SupplierId) ||
                    (int)detail.Type != row.ProgramType) throw new InvalidOperationException("اطلاعات معرفی با هویت خدمت سازگار نیست");
                if (!detail.IsActive) { row.Retire(); await db.SaveChangesAsync(ct); continue; }
                var images = new List<string>();
                foreach (var source in (detail.GalleryImages ?? []).Prepend(detail.IndexImage ?? "").Distinct().Take(20))
                {
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) Uri.TryCreate(new Uri(o.ImageBaseUrl), source, out uri);
                    if (uri?.Scheme != "https") continue;
                    try { images.Add(media.GetUrl(await media.EnsureAsync(uri.ToString(), new("Entertainments", "TouristPanel"), ct))); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { logger.LogWarning("Entertainment image unavailable ProgramKey={ProgramKey}", row.ProgramKey); }
                }
                var title = Plain(detail.Title) ?? row.Title; var description = Plain(detail.Description); var address = Plain(detail.ProgramAddress);
                var search = CatalogueText.Normalize($"{title} {description} {address} {row.CategoryTitle} {row.SupplierTitle} {row.NativeCityName} توریست پنل");
                row.Publish(title, description, Plain(detail.Rules), address, detail.IsManifestNeeded, detail.IsManifestUniqueNumberNeeded,
                    images.ToArray(), search, DateTimeOffset.UtcNow, o.CatalogRefreshMinutes);
                await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { row.DetailFailed(DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); logger.LogWarning("Entertainment detail refresh failed ProgramKey={ProgramKey}", row.ProgramKey); }
        }
        logger.LogInformation("Entertainment discovery refreshed Programs={Count}", due.Count);
    }
    public static string? Plain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = Regex.Replace(value, @"<(script|style)\b[^>]*>.*?</\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, "<[^>]*>", " ", RegexOptions.None, TimeSpan.FromSeconds(1));
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
    }
}
public sealed class TouristPanelCatalogWorker(IServiceScopeFactory scopes, IOptions<EntertainmentsOptions> options,
    ILogger<TouristPanelCatalogWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DiscoveryWorkerEnabled) return;
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var gate = scope.ServiceProvider.GetRequiredService<IEntertainmentMutationLock>();
                await using var held = await gate.AcquireAsync(new Guid("e2d8ca21-28c9-4670-a090-85b118acc1b1"), stoppingToken);
                await scope.ServiceProvider.GetRequiredService<TouristPanelCatalog>().RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Entertainment discovery sync failed ErrorType={ErrorType}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
