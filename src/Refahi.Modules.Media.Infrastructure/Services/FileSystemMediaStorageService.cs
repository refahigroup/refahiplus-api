using Microsoft.Extensions.Options;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Enums;
using Refahi.Modules.Media.Infrastructure.Options;

namespace Refahi.Modules.Media.Infrastructure.Services;

/// <summary>
/// ذخیره‌سازی فایل روی Filesystem — cross-platform (Windows/Linux).
/// </summary>
public class FileSystemMediaStorageService : IMediaStorageService
{
    private readonly MediaStorageOptions _options;

    public FileSystemMediaStorageService(IOptions<MediaStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.BasePath))
            throw new InvalidOperationException("MediaStorage:BasePath تنظیم نشده است");
    }

    public async Task<MediaStorageResult> SaveAsync(
        Stream fileStream, string fileExtension,
        MediaType mediaType, CancellationToken ct = default)
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

        await using (var fs = new FileStream(
            physicalPath, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        return new MediaStorageResult(storedFileName, relativePath);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return Task.CompletedTask;

        // جلوگیری از path traversal
        if (storagePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("مسیر فایل نامعتبر است");

        var segments = storagePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var allParts = new string[segments.Length + 1];
        allParts[0] = _options.BasePath;
        Array.Copy(segments, 0, allParts, 1, segments.Length);

        var physicalPath = Path.Combine(allParts);

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string storagePath)
        => $"{_options.CreateBaseUrl.TrimEnd('/')}/{storagePath.TrimStart('/')}";
}
