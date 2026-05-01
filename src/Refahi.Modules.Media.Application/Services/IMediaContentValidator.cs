namespace Refahi.Modules.Media.Application.Services;

/// <summary>
/// اعتبارسنجی محتوای واقعی فایل (Magic Bytes) — جلوگیری از فایل‌های جعلی.
/// </summary>
public interface IMediaContentValidator
{
    Task EnsureSafeAsync(Stream stream, string declaredContentType, CancellationToken ct = default);
}
