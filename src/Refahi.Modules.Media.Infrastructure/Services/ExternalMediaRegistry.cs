using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;
using Refahi.Modules.Media.Infrastructure.Persistence.Context;
using Source = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaSource;
using Origin = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaOrigin;
using ResolvedSource = Refahi.Modules.Media.Application.Contracts.External.ExternalMediaSource;

namespace Refahi.Modules.Media.Infrastructure.Services;

public sealed class ExternalMediaRegistry(MediaDbContext db, IExternalMediaUrls urls) : IExternalMediaService, IExternalMediaResolver
{
    private readonly HashSet<(Guid, string?, string?)> tagged = [];
    private readonly Dictionary<string, Guid> known = new(StringComparer.Ordinal);
    public static Uri? Normalize(string url) => url.Length <= 8192 && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo.Length == 0 ? uri : null;

    public async Task<string?> GetPublicUrlAsync(string sourceUrl, ExternalMediaOrigin? origin = null, CancellationToken ct = default)
        => (await GetPublicUrlsAsync([sourceUrl], origin, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> sourceUrls, ExternalMediaOrigin? origin = null, CancellationToken ct = default)
    {
        if (origin?.Module?.Length > 100 || origin?.Provider?.Length > 100) throw new ArgumentException("برچسب مدیا بیش از حد طولانی است.");
        var normalized = sourceUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize).OfType<Uri>()
            .Select(x => x.AbsoluteUri).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var batch in normalized.Where(x => !known.ContainsKey(x)).Chunk(128))
        {
            var args = new List<object>(); var rows = new List<string>();
            foreach (var url in batch)
            {
                var source = Source.Create(url, new ResolvedSource(new Uri(url)).FileName, DateTime.UtcNow);
                var offset = args.Count;
                rows.Add("(" + string.Join(",", Enumerable.Range(offset, 6).Select(i => "{" + i + "}")) + ")");
                args.AddRange([source.Id, source.SourceUrl, source.FileName, source.CreatedAt, source.StateChangedAt, (int)source.Status]);
            }
            var sql = "INSERT INTO media.external_sources (\"Id\",\"SourceUrl\",\"FileName\",\"CreatedAt\",\"StateChangedAt\",\"Status\") VALUES "
                + string.Join(",", rows) + " ON CONFLICT (\"FileName\") DO NOTHING";
            await db.Database.ExecuteSqlRawAsync(sql, args, ct);
            var hashes = batch.Select(x => new ResolvedSource(new Uri(x)).FileName).ToArray();
            var sources = await db.Set<Source>().AsNoTracking().Where(x => hashes.Contains(x.FileName)).ToArrayAsync(ct);
            foreach (var source in sources) known[source.SourceUrl] = source.Id;
        }
        if (origin is not null && normalized.Length > 0)
        {
            foreach (var batch in normalized.Where(x => !tagged.Contains((known[x], origin.Module, origin.Provider))).Chunk(128))
            {
                var args = new List<object>(); var rows = new List<string>();
                foreach (var url in batch)
                {
                    var offset = args.Count; rows.Add($"({{{offset}}},{{{offset + 1}}},{{{offset + 2}}})");
                    args.AddRange([known[url], origin.Module ?? "", origin.Provider ?? ""]);
                }
                await db.Database.ExecuteSqlRawAsync("INSERT INTO media.external_source_origins (\"SourceId\",\"Module\",\"Provider\") VALUES "
                    + string.Join(",", rows) + " ON CONFLICT DO NOTHING", args, ct);
                foreach (var url in batch) tagged.Add((known[url], origin.Module, origin.Provider));
            }
        }
        return normalized.Select(x => urls.GetUrl(known[x])).ToArray();
    }

    public async Task<ResolvedSource?> ResolveAsync(Guid id, CancellationToken ct)
    {
        var url = await db.Set<Source>().AsNoTracking().Where(x => x.Id == id).Select(x => x.SourceUrl).SingleOrDefaultAsync(ct);
        return url is null ? null : new(new Uri(url));
    }

    private static NpgsqlParameter Timestamp(int index, DateTime? value) => new("time" + index, NpgsqlDbType.TimestampTz)
        { Value = value is null ? DBNull.Value : value.Value };

    public async Task FlushAsync(IReadOnlyList<ExternalMediaObservation> changes, CancellationToken ct)
    {
        foreach (var batch in changes.Chunk(128))
        {
            var args = new List<object>(); var rows = new List<string>();
            foreach (var change in batch)
            {
                var offset = args.Count;
                rows.Add("(" + string.Join(",", Enumerable.Range(offset, 6).Select(i => "{" + i + "}")) + ")");
                args.AddRange([change.Id, change.At, Timestamp(offset + 2, change.AccessedAt), Timestamp(offset + 3, change.ExpiresAt),
                    Timestamp(offset + 4, change.RefreshAfter), (int)change.Status]);
            }
            // Explicit casts also cover batches whose optional timestamps are all NULL.
            var sql = "UPDATE media.external_sources AS s SET \"LastAccessedAt\"=GREATEST(s.\"LastAccessedAt\",v.accessed::timestamptz), "
                + "\"ExpiresAt\"=GREATEST(s.\"ExpiresAt\",v.expires::timestamptz), \"RefreshAfter\"=v.refresh::timestamptz, \"Status\"=v.status::integer, \"StateChangedAt\"=v.at::timestamptz "
                + "FROM (VALUES " + string.Join(",", rows) + ") AS v(id,at,accessed,expires,refresh,status) WHERE s.\"Id\"=v.id::uuid AND s.\"StateChangedAt\"<=v.at::timestamptz";
            await db.Database.ExecuteSqlRawAsync(sql, args, ct);
        }
    }

    public async Task<int> PurgeAsync(string? module, string? provider, ExternalMedia cache, CancellationToken ct)
    {
        module = string.IsNullOrWhiteSpace(module) ? null : module;
        provider = string.IsNullOrWhiteSpace(provider) ? null : provider;
        if (module is null && provider is null) throw new ArgumentException("حداقل یک برچسب مدیریتی الزامی است.");
        var query = db.Set<Origin>().Where(x => (module == null || x.Module == module) && (provider == null || x.Provider == provider))
            .Select(x => x.SourceId).Distinct().OrderBy(x => x);
        var count = 0;
        while (true)
        {
            var ids = await query.Skip(count).Take(128).ToArrayAsync(ct);
            if (ids.Length == 0) break;
            foreach (var id in ids) await cache.EvictAsync(id, ct);
            count += ids.Length;
        }
        return count;
    }
}
