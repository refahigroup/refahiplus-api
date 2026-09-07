using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Enums;
using Refahi.Modules.Media.Infrastructure.Options;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Media.Infrastructure.Services;

/// <summary>
/// ذخیره‌سازی فایل روی Filesystem — cross-platform (Windows/Linux).
/// </summary>
public class FileSystemMediaStorageService : IMediaStorageService
{
    private readonly MediaStorageOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, PublicUrlCacheEntry> _publicUrlCache =
        new(StringComparer.OrdinalIgnoreCase);

    public FileSystemMediaStorageService(
        IOptions<MediaStorageOptions> options,
        HttpClient httpClient
    )
    {
        _options = options.Value;
        _httpClient = httpClient;
        if (string.IsNullOrWhiteSpace(_options.BasePath))
            throw new InvalidOperationException("MediaStorage:BasePath تنظیم نشده است");
        if (
            !Uri.TryCreate(_options.LoadBaseUrl, UriKind.Absolute, out var loadBaseUri)
            || (loadBaseUri.Scheme != Uri.UriSchemeHttp && loadBaseUri.Scheme != Uri.UriSchemeHttps)
        )
            throw new InvalidOperationException(
                "MediaStorage:LoadBaseUrl باید یک آدرس مطلق HTTP یا HTTPS باشد"
            );

        foreach (var publicReadBaseUrl in _options.PublicReadBaseUrls)
            ValidatePublicBaseUrl(publicReadBaseUrl);
    }

    public async Task<MediaStorageResult> SaveAsync(
        Stream fileStream,
        string fileExtension,
        MediaType mediaType,
        CancellationToken ct = default
    )
    {
        var folder = mediaType == MediaType.Video ? "videos" : "images";
        var now = DateTimeOffset.UtcNow;
        var year = now.Year.ToString();
        var month = now.Month.ToString("D2");
        var day = now.Day.ToString("D2");

        var storedFileName = $"{Guid.NewGuid():N}{fileExtension.ToLowerInvariant()}";

        // مسیر نسبی برای DB با forward slash (cross-platform)
        var relativePath = $"{folder}/{year}/{month}/{day}/{storedFileName}";

        // مسیر فیزیکی با Path.Combine (OS-agnostic)
        var physicalDir = Path.Combine(_options.BasePath, folder, year, month, day);
        Directory.CreateDirectory(physicalDir);

        var physicalPath = Path.Combine(physicalDir, storedFileName);

        await using (
            var fs = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true
            )
        )
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        return new MediaStorageResult(storedFileName, relativePath);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return Task.CompletedTask;

        var physicalPath = ResolvePhysicalPath(storagePath);

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);

        return Task.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(
        string storagePath,
        CancellationToken ct = default
    )
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(File.Exists(ResolvePhysicalPath(storagePath)));
    }

    public async Task<string?> ResolvePublicUrlAsync(
        string storagePath,
        CancellationToken ct = default
    )
    {
        var physicalPath = ResolvePhysicalPath(storagePath);
        if (File.Exists(physicalPath))
            return GetPublicUrl(storagePath);

        if (
            _publicUrlCache.TryGetValue(storagePath, out var cached)
            && cached.ExpiresAtUtc > DateTime.UtcNow
        )
            return cached.PublicUrl;

        foreach (var publicReadBaseUrl in _options.PublicReadBaseUrls)
        {
            var publicUrl = MediaUrlBuilder.MakeAbsolute(publicReadBaseUrl, storagePath);
            if (await RemoteFileExistsAsync(publicUrl, ct))
            {
                _publicUrlCache[storagePath] = new(
                    publicUrl,
                    DateTime.UtcNow.AddHours(24)
                );
                return publicUrl;
            }
        }

        _publicUrlCache[storagePath] = new(null, DateTime.UtcNow.AddMinutes(5));
        return null;
    }

    public string GetPublicUrl(string storagePath) =>
        MediaUrlBuilder.MakeAbsolute(_options.LoadBaseUrl, storagePath);

    private string ResolvePhysicalPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new InvalidOperationException("مسیر فایل نامعتبر است");

        var normalized = storagePath.Replace('\\', '/');
        if (
            Path.IsPathRooted(normalized)
            || Uri.TryCreate(normalized, UriKind.Absolute, out _)
            || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".." || segment.Contains(':'))
        )
            throw new InvalidOperationException("مسیر فایل نامعتبر است");

        var basePath = Path.GetFullPath(_options.BasePath);
        var physicalPath = Path.GetFullPath(
            Path.Combine(basePath, normalized.Replace('/', Path.DirectorySeparatorChar))
        );
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var basePrefix = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!physicalPath.StartsWith(basePrefix, comparison))
            throw new InvalidOperationException("مسیر فایل نامعتبر است");

        return physicalPath;
    }

    private async Task<bool> RemoteFileExistsAsync(string publicUrl, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, publicUrl);
            using var headResponse = await _httpClient.SendAsync(
                head,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            if (headResponse.IsSuccessStatusCode)
                return true;
            if (headResponse.StatusCode != System.Net.HttpStatusCode.MethodNotAllowed)
                return false;

            using var get = new HttpRequestMessage(HttpMethod.Get, publicUrl);
            using var getResponse = await _httpClient.SendAsync(
                get,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            return getResponse.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static void ValidatePublicBaseUrl(string publicBaseUrl)
    {
        if (
            !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
            throw new InvalidOperationException(
                "MediaStorage:PublicReadBaseUrls باید شامل آدرس‌های مطلق HTTP یا HTTPS باشد"
            );
    }

    private readonly record struct PublicUrlCacheEntry(
        string? PublicUrl,
        DateTime ExpiresAtUtc
    );
}
