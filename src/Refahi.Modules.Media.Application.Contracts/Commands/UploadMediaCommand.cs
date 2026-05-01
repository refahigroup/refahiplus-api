using MediatR;

namespace Refahi.Modules.Media.Application.Contracts.Commands;

/// <summary>
/// آپلود یک فایل مدیا.
/// FileStream توسط Endpoint از IFormFile.OpenReadStream() ساخته می‌شود.
/// </summary>
public sealed record UploadMediaCommand(
    Stream FileStream,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    Guid UploadedByUserId,
    string? EntityType = null,
    Guid? EntityId = null
) : IRequest<UploadMediaResponse>;

public sealed record UploadMediaResponse(
    Guid MediaId,
    string Url,
    string ContentType,
    long FileSizeBytes
);
