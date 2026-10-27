using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Contracts.External;

namespace Refahi.Modules.Media.Infrastructure.Services;

public sealed class PersistentExternalMediaOptions
{
    public bool Enabled { get; set; }
    public string StoragePath { get; set; } = "";
    public long MinFreeBytes { get; set; } = 2L << 30;
}

public sealed class PersistentExternalMedia(ExternalMedia download, IOptions<ExternalMediaOptions> external,
    IOptions<PersistentExternalMediaOptions> options) : IPersistentExternalMedia
{
    private readonly SemaphoreSlim writes = new(1);
    private string Root => Path.GetFullPath(options.Value.StoragePath);
    public string GetUrl(Guid id) => external.Value.PublicBaseUrl.TrimEnd('/') + "/media/persistent/" + id.ToString("N");
    public bool Exists(Guid id) => options.Value.Enabled && !string.IsNullOrWhiteSpace(options.Value.StoragePath)
        && File.Exists(Path.Combine(Root, id.ToString("N") + ".webp"));
    public Stream? Open(Guid id)
    {
        try { return Exists(id) ? new FileStream(Path.Combine(Root, id.ToString("N") + ".webp"), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public async Task<Guid> EnsureAsync(string sourceUrl, ExternalMediaOrigin origin, CancellationToken ct)
    {
        if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.StoragePath))
            throw new InvalidOperationException("ذخیره پایدار مدیا فعال نیست.");
        var uri = ExternalMediaRegistry.Normalize(sourceUrl) ?? throw new ArgumentException("نشانی تصویر معتبر نیست.");
        var source = new ExternalMediaSource(uri);
        if (!download.Allowed(source)) throw new ExternalMediaException(502);
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)).AsSpan(0, 16));
        var target = Path.Combine(Root, id.ToString("N") + ".webp");
        if (Exists(id) && File.GetLastWriteTimeUtc(target) > DateTime.UtcNow.AddHours(-external.Value.FreshHours)) return id;
        await writes.WaitAsync(ct);
        string? input = null, output = null;
        try
        {
            Directory.CreateDirectory(Root);
            // The shared persistent volume is coordinated across processes, independently of the transient cache.
            await using var held = new FileStream(target + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (Exists(id) && File.GetLastWriteTimeUtc(target) > DateTime.UtcNow.AddHours(-external.Value.FreshHours)) return id;
            var drive = DriveInfo.GetDrives().Where(d => Root.StartsWith(d.Name, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                .OrderByDescending(d => d.Name.Length).First();
            if (drive.AvailableFreeSpace < options.Value.MinFreeBytes || drive.AvailableFreeSpace - options.Value.MinFreeBytes < (long)external.Value.MaxSourceBytes + external.Value.MaxImageBytes)
                throw new ExternalMediaException(503, "PersistentStorageFull");
            var suffix = Guid.NewGuid().ToString("N");
            input = target + "." + suffix + ".input"; output = target + "." + suffix + ".part";
            using var image = await download.OpenAsync(id, source, ct);
            await using (var file = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920]; long bytes = 0; int count;
                while ((count = await image.Stream.ReadAsync(buffer, ct)) > 0)
                {
                    bytes += count;
                    if (bytes > external.Value.MaxSourceBytes) throw new ExternalMediaException(502, "ImageTooLarge");
                    await file.WriteAsync(buffer.AsMemory(0, count), ct);
                }
            }
            ExternalImageOptimizer.Optimize(input, output, external.Value, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(output, target, overwrite: true);
            return id;
        }
        finally
        {
            try { if (input is not null) File.Delete(input); if (output is not null) File.Delete(output); }
            finally { writes.Release(); }
        }
    }
}
