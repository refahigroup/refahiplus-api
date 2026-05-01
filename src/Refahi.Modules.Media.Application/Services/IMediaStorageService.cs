using Refahi.Modules.Media.Domain.Enums;

namespace Refahi.Modules.Media.Application.Services;

/// <summary>
/// انتزاع ذخیره‌سازی فایل — قابل‌جایگزینی با Object Storage در آینده.
/// </summary>
public interface IMediaStorageService
{
    Task<MediaStorageResult> SaveAsync(
        Stream fileStream,
        string fileExtension,
        MediaType mediaType,
        CancellationToken ct = default);

    Task DeleteAsync(string storagePath, CancellationToken ct = default);

    string GetPublicUrl(string storagePath);
}

public sealed record MediaStorageResult(
    string StoredFileName,    // مثلاً "abc123.jpg"
    string StoragePath        // نسبی، با /: "images/2026/04/29/abc123.jpg"
);
