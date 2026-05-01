namespace Refahi.Modules.Media.Application.Contracts.DTOs;

public sealed record MediaAssetDto(
    Guid Id,
    string OriginalFileName,
    string Url,                    // محاسبه‌شده از StoragePath + BaseUrl
    string ContentType,
    long FileSizeBytes,
    string MediaType,              // "Image" | "Video"
    string? EntityType,
    Guid? EntityId,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt
);
