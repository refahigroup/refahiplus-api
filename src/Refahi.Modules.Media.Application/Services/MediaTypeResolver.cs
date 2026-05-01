using Refahi.Modules.Media.Domain.Enums;
using Refahi.Modules.Media.Domain.Exceptions;

namespace Refahi.Modules.Media.Application.Services;

/// <summary>
/// نوع و extension تأییدشده را از ContentType استخراج می‌کند.
/// خروجی این متد منبع موثق برای ساختن نام فایل است (نه filename کاربر).
/// </summary>
public static class MediaTypeResolver
{
    public static (MediaType MediaType, string Extension) Resolve(string contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => (MediaType.Image, ".jpg"),
            "image/jpg" => (MediaType.Image, ".jpg"),
            "image/png" => (MediaType.Image, ".png"),
            "image/webp" => (MediaType.Image, ".webp"),
            "image/gif" => (MediaType.Image, ".gif"),
            "video/mp4" => (MediaType.Video, ".mp4"),
            "video/webm" => (MediaType.Video, ".webm"),
            _ => throw new MediaDomainException("نوع فایل پشتیبانی نمی‌شود", "MEDIA_UNSUPPORTED_TYPE")
        };
    }
}
