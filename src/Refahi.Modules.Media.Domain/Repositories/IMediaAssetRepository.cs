using Refahi.Modules.Media.Domain.Aggregates;

namespace Refahi.Modules.Media.Domain.Repositories;

public interface IMediaAssetRepository
{
    Task AddAsync(MediaAsset asset, CancellationToken ct = default);

    Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<MediaAsset> Items, int Total)> ListAsync(
        int page,
        int size,
        string? entityType,
        Guid? uploadedBy,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
