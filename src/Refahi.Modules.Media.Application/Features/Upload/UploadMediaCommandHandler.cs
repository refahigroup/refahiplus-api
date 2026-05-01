using MediatR;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Aggregates;
using Refahi.Modules.Media.Domain.Exceptions;
using Refahi.Modules.Media.Domain.Repositories;

namespace Refahi.Modules.Media.Application.Features.Upload;

public class UploadMediaCommandHandler : IRequestHandler<UploadMediaCommand, UploadMediaResponse>
{
    private readonly IMediaStorageService _storage;
    private readonly IMediaAssetRepository _repository;
    private readonly IMediaContentValidator _contentValidator;

    public UploadMediaCommandHandler(
        IMediaStorageService storage,
        IMediaAssetRepository repository,
        IMediaContentValidator contentValidator)
    {
        _storage = storage;
        _repository = repository;
        _contentValidator = contentValidator;
    }

    public async Task<UploadMediaResponse> Handle(UploadMediaCommand request, CancellationToken ct)
    {
        // ۱. derive نوع و extension تأییدشده از ContentType (نه از filename)
        var (mediaType, extension) = MediaTypeResolver.Resolve(request.ContentType);

        // ۲. اعتبارسنجی محتوای واقعی فایل (Magic Bytes)
        await _contentValidator.EnsureSafeAsync(request.FileStream, request.ContentType, ct);

        // ریست stream بعد از خواندن header
        if (request.FileStream.CanSeek)
            request.FileStream.Position = 0;

        // ۳. ذخیره فیزیکی روی دیسک
        Refahi.Modules.Media.Application.Services.MediaStorageResult storageResult;
        try
        {
            storageResult = await _storage.SaveAsync(
                request.FileStream, extension, mediaType, ct);
        }
        catch (MediaDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MediaDomainException(
                "ذخیره فایل با خطا مواجه شد: " + ex.Message,
                "MEDIA_STORAGE_FAILED");
        }

        // ۴. ثبت در DB با compensating action
        try
        {
            var asset = MediaAsset.Create(
                request.OriginalFileName,
                storageResult.StoredFileName,
                storageResult.StoragePath,
                request.ContentType,
                request.FileSizeBytes,
                mediaType,
                request.UploadedByUserId,
                request.EntityType,
                request.EntityId);

            await _repository.AddAsync(asset, ct);
            await _repository.SaveChangesAsync(ct);

            return new UploadMediaResponse(
                asset.Id,
                _storage.GetPublicUrl(asset.StoragePath),
                asset.ContentType,
                asset.FileSizeBytes);
        }
        catch
        {
            // Compensating action: حذف فایل فیزیکی اگر DB fail شد
            try
            {
                await _storage.DeleteAsync(storageResult.StoragePath, CancellationToken.None);
            }
            catch
            {
                // لاگ شود اما اولویت ندارد — fail اصلی DB است
            }
            throw;
        }
    }
}
