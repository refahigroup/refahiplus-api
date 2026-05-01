using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Exceptions;

namespace Refahi.Modules.Media.Infrastructure.Services;

/// <summary>
/// تأیید signature واقعی فایل از طریق Magic Bytes — جلوگیری از فایل‌های جعلی.
/// </summary>
public class MagicBytesContentValidator : IMediaContentValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/jpg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47]],
        ["image/gif"] = [[0x47, 0x49, 0x46, 0x38]],
        ["image/webp"] = [[0x52, 0x49, 0x46, 0x46]],   // RIFF header — TD-MEDIA-06 برای بررسی WEBP chunk
    };

    public async Task EnsureSafeAsync(Stream stream, string declaredContentType, CancellationToken ct = default)
    {
        // ویدیو فعلاً lenient — ftyp box در offset متغیر — TD-MEDIA-06
        if (declaredContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return;

        if (!Signatures.TryGetValue(declaredContentType, out var sigs))
            return;  // نوع‌های ناشناخته توسط Validator/Resolver رد شده‌اند

        var buffer = new byte[16];
        var read = await stream.ReadAsync(buffer.AsMemory(0, 16), ct);

        if (read < 4)
            throw new MediaDomainException("فایل نامعتبر یا خالی است", "MEDIA_INVALID_CONTENT");

        bool match = false;
        foreach (var sig in sigs)
        {
            if (read >= sig.Length && buffer.Take(sig.Length).SequenceEqual(sig))
            {
                match = true;
                break;
            }
        }

        if (!match)
            throw new MediaDomainException(
                "محتوای فایل با نوع اعلام‌شده مطابقت ندارد",
                "MEDIA_CONTENT_MISMATCH");
    }
}
