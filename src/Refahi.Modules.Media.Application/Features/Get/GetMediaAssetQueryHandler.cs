using MediatR;
using Refahi.Modules.Media.Application.Contracts.DTOs;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Repositories;

namespace Refahi.Modules.Media.Application.Features.Get;

public class GetMediaAssetQueryHandler : IRequestHandler<GetMediaAssetQuery, MediaAssetDto?>
{
    private readonly IMediaAssetRepository _repository;
    private readonly IMediaStorageService _storage;

    public GetMediaAssetQueryHandler(
        IMediaAssetRepository repository,
        IMediaStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<MediaAssetDto?> Handle(GetMediaAssetQuery request, CancellationToken ct)
    {
        var asset = await _repository.GetByIdAsync(request.Id, ct);
        if (asset is null || asset.IsDeleted) return null;

        return new MediaAssetDto(
            asset.Id,
            asset.OriginalFileName,
            _storage.GetPublicUrl(asset.StoragePath),
            asset.ContentType,
            asset.FileSizeBytes,
            asset.MediaType.ToString(),
            asset.EntityType,
            asset.EntityId,
            asset.UploadedByUserId,
            asset.UploadedAt);
    }
}
