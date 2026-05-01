using MediatR;
using Refahi.Modules.Media.Application.Contracts.DTOs;
using Refahi.Modules.Media.Application.Contracts.Queries;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Repositories;

namespace Refahi.Modules.Media.Application.Features.List;

public class ListMediaAssetsQueryHandler : IRequestHandler<ListMediaAssetsQuery, ListMediaAssetsResult>
{
    private readonly IMediaAssetRepository _repository;
    private readonly IMediaStorageService _storage;

    public ListMediaAssetsQueryHandler(
        IMediaAssetRepository repository,
        IMediaStorageService storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<ListMediaAssetsResult> Handle(ListMediaAssetsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.Size < 1 ? 20 : Math.Min(request.Size, 100);

        var (items, total) = await _repository.ListAsync(
            page, size, request.EntityType, request.UploadedBy, ct);

        var dtos = items.Select(asset => new MediaAssetDto(
            asset.Id,
            asset.OriginalFileName,
            _storage.GetPublicUrl(asset.StoragePath),
            asset.ContentType,
            asset.FileSizeBytes,
            asset.MediaType.ToString(),
            asset.EntityType,
            asset.EntityId,
            asset.UploadedByUserId,
            asset.UploadedAt)).ToList();

        return new ListMediaAssetsResult(dtos, total, page, size);
    }
}
