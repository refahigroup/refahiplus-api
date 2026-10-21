using ExternalMediaStatus = Refahi.Modules.Media.Domain.Aggregates.ExternalMediaStatus;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;

namespace Refahi.Modules.Media.Infrastructure.Services;

public sealed class ExternalMediaOptions
{
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";
    public string CachePath { get; set; } = Path.Combine(Path.GetTempPath(), "refahi-external-media");
    public bool CacheEnabled { get; set; } = true;
    public long MaxBytes { get; set; } = 1L << 30;
    public long MinFreeBytes { get; set; } = 2L << 30;
    public int MaxImageBytes { get; set; } = 5 << 20;
    public int ConcurrentDownloads { get; set; } = 4;
    public int QueueLimit { get; set; } = 32;
    public int MaxEntries { get; set; } = 20000;
    public int FreshHours { get; set; } = 24;
    public int SlidingExpirationHours { get; set; } = 5;
    public int ActivityFlushMinutes { get; set; } = 5;
    public int CleanupMinutes { get; set; } = 5;
    public int QueueWaitSeconds { get; set; } = 2;
    public int ConnectSeconds { get; set; } = 3;
    public int DownloadSeconds { get; set; } = 15;
    public int NegativeSeconds { get; set; } = 30;
    public string[] AllowedHosts { get; set; } = ["store.snapptrip.com"];
}
public sealed class ExternalMediaException(int status) : Exception("تصویر در دسترس نیست.") { public int Status { get; } = status; }
public sealed class ExternalImage(Stream stream, string type, string? etag, IDisposable? owner = null) : IDisposable
{
    public Stream Stream { get; } = stream;
    public string ContentType { get; } = type;
    public string? ETag { get; } = etag;
    public void Dispose() { Stream.Dispose(); owner?.Dispose(); }
}
public sealed class ExternalMedia : BackgroundService, IExternalMediaUrls
{
    private readonly Meter meter = new("Refahi.ExternalMedia");
    private readonly Counter<long> events;
    private readonly ILogger<ExternalMedia> logger;
    private readonly ExternalMediaOptions o;
    private readonly IExternalMediaActivity? activity;
    private readonly HashSet<Guid> pendingEvictions = [];
    private readonly TimeProvider clock;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private readonly HttpClient client;
    private readonly SemaphoreSlim slots;
    private readonly SemaphoreSlim[] stripes = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = new();
    private readonly Dictionary<Guid, DateTime> failures = new();
    private FileStream? ownership;
    private long reserved;
    private readonly Dictionary<Guid, int> readers = [];
    private int waiting;
    private bool writable;
    private sealed record Entry(Guid Id, string File, string Type, string ETag, long Bytes, DateTime RefreshAfter, DateTime Access, DateTime ExpiresAt);
    public ExternalMedia(IOptions<ExternalMediaOptions> options, HttpMessageHandler? handler = null, TimeProvider? time = null, ILogger<ExternalMedia>? log = null, IExternalMediaActivity? activity = null)
    {
        this.activity = activity;
        logger = log ?? NullLogger<ExternalMedia>.Instance;
        events = meter.CreateCounter<long>("external_media.events");
        meter.CreateObservableGauge("external_media.cache_bytes", () => { lock (gate) return entries.Values.Sum(x => x.Bytes); });
        meter.CreateObservableGauge("external_media.cache_entries", () => { lock (gate) return entries.Count; });
        meter.CreateObservableGauge("external_media.reserved_bytes", () => { lock (gate) return reserved; });
        meter.CreateObservableGauge("external_media.waiting", () => Volatile.Read(ref waiting));
        clock = time ?? TimeProvider.System; o = options.Value; slots = new(o.ConcurrentDownloads);
        client = new(handler ?? new SocketsHttpHandler {
            AllowAutoRedirect = false, UseCookies = false, UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(o.ConnectSeconds), ConnectCallback = ConnectAsync
        }) { Timeout = Timeout.InfiniteTimeSpan };
    }
    public string GetUrl(Guid id) => o.PublicBaseUrl.TrimEnd('/') + "/media/external/" + id.ToString("N");
    public static bool PublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xe0) == 0x20
                && !(b[0] == 0x20 && b[1] == 1 && (b[2] < 2 || b[2] == 0x0d && b[3] == 0xb8))
                && !(b[0] == 0x20 && b[1] == 2) && !(b[0] == 0x3f && b[1] == 0xff);
        return b[0] is not (0 or 10 or 127) && b[0] < 224
            && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2 || b[1] == 88 && b[2] == 99))
            && !(b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            && !(b[0] == 198 && (b[1] is 18 or 19 or 51)) && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        if (addresses.Length == 0 || addresses.Any(x => !PublicAddress(x))) throw new ExternalMediaException(502);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try { await socket.ConnectAsync(new IPEndPoint(addresses[0], context.DnsEndPoint.Port), ct); return new NetworkStream(socket, true); }
        catch { socket.Dispose(); throw; }
    }
    public bool Allowed(ExternalMediaSource source) => source.Url.Scheme == "https" && source.Url.Port == 443
        && source.Url.UserInfo.Length == 0 && !IPAddress.TryParse(source.Url.Host, out _)
        && o.AllowedHosts.Contains(source.Url.IdnHost, StringComparer.OrdinalIgnoreCase);
    public override Task StartAsync(CancellationToken ct)
    {
        if (o.CacheEnabled)
        {
            try
            {
                for (var parent = new DirectoryInfo(Path.GetFullPath(o.CachePath)); parent is not null; parent = parent.Parent)
                    if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Unsafe cache directory");
                Directory.CreateDirectory(o.CachePath);
                if ((File.GetAttributes(o.CachePath) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("مسیر کش نامعتبر است.");
                ownership = new(Path.Combine(o.CachePath, ".owner"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                writable = true;
                foreach (var file in Directory.EnumerateFiles(o.CachePath))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("فایل کش نامعتبر است.");
                    if (file.EndsWith(".part", StringComparison.Ordinal)) File.Delete(file);
                    if (!file.EndsWith(".json", StringComparison.Ordinal)) continue;
                    try {
                        var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(file));
                        if (entry is not null && entry.Id != Guid.Empty && File.Exists(entry.File)
                            && Path.GetFullPath(entry.File) == Path.Combine(Path.GetFullPath(o.CachePath), Path.GetFileNameWithoutExtension(file) + ".bin")
                            && Path.GetFileNameWithoutExtension(file).Length == 64 && Path.GetFileNameWithoutExtension(file).All(Uri.IsHexDigit))
                        {
                            var access = File.GetLastWriteTimeUtc(entry.File);
                            entries[entry.Id] = entry with { Access = access, ExpiresAt = access.AddHours(o.SlidingExpirationHours), Bytes = new FileInfo(entry.File).Length + 4096 };
                        }
                        else File.Delete(file);
                    } catch (JsonException) { File.Delete(file); }
                }
                foreach (var file in Directory.EnumerateFiles(o.CachePath, "*.bin"))
                    if (!entries.Values.Any(x => x.File == Path.GetFullPath(file))) File.Delete(file);
                Sweep();
            }
            catch (UnauthorizedAccessException) { writable = false; ownership?.Dispose(); }
            // A locked directory must fail startup; it belongs to another process.
        }
        return base.StartAsync(ct);
    }
    public ExternalImage? TryOpenCached(Guid id) => Hit(id);
    private ExternalImage? Hit(Guid id)
    {
        lock (gate)
        {
            if (pendingEvictions.Contains(id) || !entries.TryGetValue(id, out var e) || Now >= e.RefreshAfter || Now >= e.ExpiresAt) return null;
            try
            {
                var stream = new FileStream(e.File, FileMode.Open, FileAccess.Read, FileShare.Read);
                var accessed = Now;
                entries[id] = e with { Access = accessed, ExpiresAt = accessed.AddHours(o.SlidingExpirationHours) };
                readers[id] = readers.GetValueOrDefault(id) + 1;
                Observe(id, ExternalMediaStatus.Cached, accessed, e.RefreshAfter);
                return new(stream, e.Type, e.ETag, new OnDispose(() =>
                {
                    lock (gate)
                    {
                        if (--readers[id] == 0)
                        {
                            readers.Remove(id);
                            if (pendingEvictions.Remove(id) && entries.TryGetValue(id, out var entry)) Remove(id, entry);
                        }
                    }
                }));
            }
            catch (IOException) { return null; }
        }
    }
    private void Remove(Guid id, Entry e)
    {
        if (readers.ContainsKey(id)) { pendingEvictions.Add(id); return; }
        try { File.Delete(e.File); File.Delete(Path.ChangeExtension(e.File, ".json")); entries.Remove(id); Observe(id, ExternalMediaStatus.NotCached); events.Add(1, new KeyValuePair<string, object?>("kind", "eviction")); } catch (IOException) { }
    }
    internal void Sweep(long required = 0)
    {
        lock (gate)
        {
            foreach (var pair in entries.OrderBy(x => x.Value.Access).ToArray())
            {
                if (Now >= pair.Value.ExpiresAt || entries.Count >= o.MaxEntries && required > 0 || entries.Count > o.MaxEntries || entries.Values.Sum(x => x.Bytes) + reserved + required > o.MaxBytes * .9)
                    Remove(pair.Key, pair.Value);
                else
                {
                    try { File.SetLastWriteTimeUtc(pair.Value.File, pair.Value.Access); }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
    public async Task<ExternalImage> OpenAsync(Guid id, ExternalMediaSource source, CancellationToken ct)
    {
        if (!Allowed(source)) throw new ExternalMediaException(502);
        if (Hit(id) is { } hit) { events.Add(1, new KeyValuePair<string, object?>("kind", "hit")); return hit; }
        Observe(id, ExternalMediaStatus.NotCached, Now);
        events.Add(1, new KeyValuePair<string, object?>("kind", "miss"));
        if (Interlocked.Increment(ref waiting) > o.QueueLimit + o.ConcurrentDownloads) { Interlocked.Decrement(ref waiting); throw new ExternalMediaException(503); }
        var stripe = stripes[(int)((uint)id.GetHashCode() % (uint)stripes.Length)];
        var held = false; var slot = false; var transferred = false;
        try
        {
            using var queue = CancellationTokenSource.CreateLinkedTokenSource(ct); queue.CancelAfter(TimeSpan.FromSeconds(o.QueueWaitSeconds));
            await stripe.WaitAsync(queue.Token); held = true;
            if (Hit(id) is { } cached) return cached;
            lock (gate) { if (failures.TryGetValue(id, out var until) && until > Now) throw new ExternalMediaException(502); }
            await slots.WaitAsync(queue.Token); slot = true;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(o.DownloadSeconds));
            var uri = source.Url;
            HttpResponseMessage? response = null;
            try
            {
                for (var redirects = 0; ; redirects++)
                {
                    if (!Allowed(new(uri))) throw new ExternalMediaException(502);
                    response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                    if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) break;
                    var location = response.Headers.Location; response.Dispose();
                    if (redirects == 2 || location is null) throw new ExternalMediaException(502);
                    uri = new Uri(uri, location);
                }
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > o.MaxImageBytes) throw new ExternalMediaException(502);
                var type = response.Content.Headers.ContentType?.MediaType ?? "";
                var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                var prefix = new byte[12]; var length = 0;
                while (length < prefix.Length) { var n = await stream.ReadAsync(prefix.AsMemory(length), deadline.Token); if (n == 0) break; length += n; }
                Validate(prefix[..length], type);
                var target = Path.Combine(Path.GetFullPath(o.CachePath), source.FileName + ".bin");
                var save = false;
                lock (gate)
                {
                    Sweep(o.MaxImageBytes + 4096);
                    if (writable && entries.Count < o.MaxEntries && entries.Values.Sum(x => x.Bytes) + reserved + o.MaxImageBytes + 8192 <= o.MaxBytes
                        && new DriveInfo(Path.GetPathRoot(Path.GetFullPath(o.CachePath))!).AvailableFreeSpace >= o.MinFreeBytes + o.MaxImageBytes + 4096)
                    { reserved += o.MaxImageBytes + 4096; save = true; }
                }
                if (save)
                {
                    try
                    {
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        long total = length;
                        await using (var output = new FileStream(target + ".part", FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await output.WriteAsync(prefix.AsMemory(0, length), deadline.Token); hash.AppendData(prefix.AsSpan(0, length));
                            var buffer = new byte[81920]; int n;
                            while ((n = await stream.ReadAsync(buffer, deadline.Token)) != 0)
                            { total += n; if (total > o.MaxImageBytes) throw new ExternalMediaException(502); hash.AppendData(buffer.AsSpan(0,n)); await output.WriteAsync(buffer.AsMemory(0,n), deadline.Token); }
                        }
                        var etag = "\"" + Convert.ToHexString(hash.GetHashAndReset()) + "\"";
                        lock (gate)
                        {
                            if (readers.ContainsKey(id)) throw new ExternalMediaException(503);
                            File.Move(target + ".part", target, true);
                            var entry = new Entry(id, target, type, etag, total + 4096, Now.AddHours(o.FreshHours), Now, Now.AddHours(o.SlidingExpirationHours));
                            File.WriteAllText(Path.ChangeExtension(target, ".json"), JsonSerializer.Serialize(entry)); entries[id] = entry;
                        }
                        return Hit(id) ?? throw new ExternalMediaException(502);
                    }
                    finally { lock (gate) { reserved -= o.MaxImageBytes + 4096; try { File.Delete(target + ".part"); } catch (IOException) { } } }
                }
                // The response owns its upstream slot until the browser finishes reading.
                var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct); lifetime.CancelAfter(TimeSpan.FromSeconds(o.DownloadSeconds));
                var ownedResponse = response; response = null; slot = false; transferred = true;
                Observe(id, ExternalMediaStatus.NotCached, Now);
                return new(new LimitedImageStream(stream, prefix[..length], o.MaxImageBytes, lifetime,
                    () => { ownedResponse.Dispose(); slots.Release(); Interlocked.Decrement(ref waiting); }), type, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalMediaException(504); }
            finally { response?.Dispose(); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ExternalMediaException(503); }
        catch (HttpRequestException) { throw new ExternalMediaException(502); }
        catch (IOException) { throw new ExternalMediaException(502); }
        catch (UnauthorizedAccessException) { throw new ExternalMediaException(502); }
        catch (ExternalMediaException ex)
        {
            logger.LogWarning("External image failure MediaId={MediaId} Status={Status}", id, ex.Status);
            Observe(id, ExternalMediaStatus.Failed);
            events.Add(1, new KeyValuePair<string, object?>("kind", "failure"));
            lock (gate) { if (failures.Count >= o.MaxEntries) failures.Clear(); failures[id] = Now.AddSeconds(30); }
            throw;
        }
        finally { if (slot) slots.Release(); if (held) stripe.Release(); if (!transferred) Interlocked.Decrement(ref waiting); }
    }
    private void Observe(Guid id, ExternalMediaStatus status, DateTime? accessed = null, DateTime? refreshAfter = null)
        => activity?.Record(new(id, Now, accessed, accessed?.AddHours(o.SlidingExpirationHours), refreshAfter, status));

    public async Task EvictAsync(Guid id, CancellationToken ct)
    {
        var stripe = stripes[(int)((uint)id.GetHashCode() % (uint)stripes.Length)];
        await stripe.WaitAsync(ct);
        try
        {
            lock (gate)
            {
                if (entries.TryGetValue(id, out var entry)) Remove(id, entry);
                failures.Remove(id);
                Observe(id, ExternalMediaStatus.NotCached);
            }
        }
        finally { stripe.Release(); }
    }
    public static void Validate(byte[] b, string type)
    {
        bool ok = type switch {
            "image/jpeg" or "image/jpg" => b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255,
            "image/png" => b.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}),
            "image/gif" => b.AsSpan().StartsWith("GIF87a"u8) || b.AsSpan().StartsWith("GIF89a"u8),
            "image/webp" => b.Length >= 12 && b.AsSpan(0,4).SequenceEqual("RIFF"u8) && b.AsSpan(8,4).SequenceEqual("WEBP"u8), _ => false };
        if (!ok) throw new ExternalMediaException(502);
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    { using var timer = new PeriodicTimer(TimeSpan.FromMinutes(o.CleanupMinutes)); while (await timer.WaitForNextTickAsync(ct)) Sweep(); }
    public override void Dispose() { meter.Dispose(); client.Dispose(); ownership?.Dispose(); base.Dispose(); }
}

internal sealed class LimitedImageStream(Stream upstream, byte[] prefix, long limit, CancellationTokenSource lifetime, Action release) : Stream
{
    private int position; private long count; private int disposed;
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        int n;
        if (position < prefix.Length) { n = Math.Min(buffer.Length, prefix.Length - position); prefix.AsMemory(position,n).CopyTo(buffer); position += n; }
        else n = await upstream.ReadAsync(buffer[..(int)Math.Min(buffer.Length, Math.Max(1, limit-count+1))], linked.Token);
        count += n; if (count > limit) throw new IOException("تصویر بیش از حد مجاز است."); return n;
    }
    protected override void Dispose(bool disposing) { if (disposing && Interlocked.Exchange(ref disposed,1)==0) { upstream.Dispose(); lifetime.Dispose(); release(); } base.Dispose(disposing); }
    public override int Read(byte[] b,int o,int c) => ReadAsync(b.AsMemory(o,c)).AsTask().GetAwaiter().GetResult();
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => count; set => throw new NotSupportedException(); }
    public override void Flush() { } public override long Seek(long o,SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long l) => throw new NotSupportedException(); public override void Write(byte[] b,int o,int c) => throw new NotSupportedException();
}

internal sealed class OnDispose(Action release) : IDisposable { public void Dispose() => release(); }
