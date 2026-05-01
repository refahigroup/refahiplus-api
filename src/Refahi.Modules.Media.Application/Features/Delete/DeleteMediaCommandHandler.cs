using MediatR;
using Refahi.Modules.Media.Application.Contracts.Commands;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Repositories;

namespace Refahi.Modules.Media.Application.Features.Delete;

public class DeleteMediaCommandHandler : IRequestHandler<DeleteMediaCommand>
{
    private readonly IMediaAssetRepository _repository;
    private readonly IMediaStorageService _storage;

    public DeleteMediaCommandHandler(
        IMediaAssetRepository repository,
        IMediaStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task Handle(DeleteMediaCommand request, CancellationToken ct)
    {
        var asset = await _repository.GetByIdAsync(request.MediaId, ct)
            ?? throw new KeyNotFoundException("فایل مدیا یافت نشد");

        if (asset.IsDeleted) return;

        if (!request.IsAdmin && asset.UploadedByUserId != request.RequestedByUserId)
            throw new UnauthorizedAccessException("اجازه حذف این فایل را ندارید");

        asset.SoftDelete();
        await _repository.SaveChangesAsync(ct);

        // حذف فیزیکی پس از ثبت soft delete (Cleanup Job در آینده فایل‌های جامانده را پاک می‌کند)
        try
        {
            await _storage.DeleteAsync(asset.StoragePath, ct);
        }
        catch
        {
            // ignore — TD-MEDIA-05 (Cleanup Job)
        }
    }
}
