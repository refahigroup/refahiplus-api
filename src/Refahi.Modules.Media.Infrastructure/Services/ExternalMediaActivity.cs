using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Domain.Aggregates;

namespace Refahi.Modules.Media.Infrastructure.Services;

public sealed record ExternalMediaObservation(Guid Id, DateTime At, DateTime? AccessedAt, DateTime? ExpiresAt,
    DateTime? RefreshAfter, ExternalMediaStatus Status);

// One bounded pending observation per file; a cache hit never writes to the database.
public interface IExternalMediaActivity { void Record(ExternalMediaObservation change); }

public sealed class ExternalMediaActivity(IServiceScopeFactory scopes, IOptions<ExternalMediaOptions> options,
    ILogger<ExternalMediaActivity> logger) : BackgroundService, IExternalMediaActivity
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, ExternalMediaObservation> pending = [];
    private readonly SemaphoreSlim flush = new(1);
    private readonly SemaphoreSlim wake = new(0, 1);
    private long overflow;
    public void Record(ExternalMediaObservation change)
    {
        lock (gate)
        {
            if (pending.TryGetValue(change.Id, out var old))
                change = change with { AccessedAt = Later(old.AccessedAt, change.AccessedAt), ExpiresAt = Later(old.ExpiresAt, change.ExpiresAt) };
            else if (pending.Count >= options.Value.MaxEntries)
            {
                Interlocked.Increment(ref overflow);
                if (wake.CurrentCount == 0) wake.Release();
                return;
            }
            pending[change.Id] = change;
            if (pending.Count == options.Value.MaxEntries && wake.CurrentCount == 0) wake.Release();
        }
    }
    private static DateTime? Later(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a > b ? a : b;
    public async Task FlushAsync(CancellationToken ct)
    {
        await flush.WaitAsync(ct);
        try
        {
            ExternalMediaObservation[] changes;
            lock (gate) changes = pending.Values.ToArray();
            if (changes.Length == 0) return;
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ExternalMediaRegistry>().FlushAsync(changes, ct);
            var lost = Interlocked.Exchange(ref overflow, 0);
            if (lost > 0) logger.LogWarning("External media activity buffer pressure DroppedObservations={Count}", lost);
            lock (gate) foreach (var change in changes)
                if (pending.TryGetValue(change.Id, out var current) && current == change) pending.Remove(change.Id);
        }
        finally { flush.Release(); }
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await wake.WaitAsync(TimeSpan.FromMinutes(options.Value.ActivityFlushMinutes), ct); await FlushAsync(ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { logger.LogWarning("External media activity flush failed ErrorType={ErrorType}", ex.GetType().Name); }
        }
    }
    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        try { await FlushAsync(ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning("External media activity flush failed ErrorType={ErrorType}", ex.GetType().Name); }
    }
}
