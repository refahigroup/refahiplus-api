using Refahi.Modules.Media.Domain.Enums;
using Refahi.Modules.Media.Domain.Exceptions;

namespace Refahi.Modules.Media.Domain.Aggregates;

public sealed class MediaAsset
{
    private MediaAsset() { }

    public Guid Id { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StoredFileName { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;        // نسبی، با /
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public MediaType MediaType { get; private set; }
    public string? EntityType { get; private set; }                        // از MediaEntityTypes
    public Guid? EntityId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static MediaAsset Create(
        string originalFileName,
        string storedFileName,
        string storagePath,
        string contentType,
        long fileSizeBytes,
        MediaType mediaType,
        Guid uploadedByUserId,
        string? entityType = null,
        Guid? entityId = null)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            throw new MediaDomainException("نام فایل ذخیره‌شده الزامی است", "MEDIA_STORED_NAME_REQUIRED");
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new MediaDomainException("مسیر ذخیره‌سازی الزامی است", "MEDIA_STORAGE_PATH_REQUIRED");
        if (string.IsNullOrWhiteSpace(contentType))
            throw new MediaDomainException("نوع محتوا الزامی است", "MEDIA_CONTENT_TYPE_REQUIRED");
        if (fileSizeBytes <= 0)
            throw new MediaDomainException("حجم فایل نامعتبر است", "MEDIA_INVALID_SIZE");
        if (uploadedByUserId == Guid.Empty)
            throw new MediaDomainException("شناسه کاربر آپلودکننده الزامی است", "MEDIA_UPLOADER_REQUIRED");

        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName.Trim(),
            StoredFileName = storedFileName,
            StoragePath = storagePath,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            MediaType = mediaType,
            EntityType = entityType,
            EntityId = entityId,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };
    }

    public void LinkToEntity(string entityType, Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new MediaDomainException("نوع entity الزامی است", "MEDIA_ENTITY_TYPE_REQUIRED");
        if (entityId == Guid.Empty)
            throw new MediaDomainException("شناسه entity الزامی است", "MEDIA_ENTITY_ID_REQUIRED");

        EntityType = entityType;
        EntityId = entityId;
    }

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
