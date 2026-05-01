using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Media.Domain.Aggregates;
using Refahi.Modules.Media.Domain.Repositories;
using Refahi.Modules.Media.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Media.Infrastructure.Repositories;

public class MediaAssetRepository : IMediaAssetRepository
{
    private readonly MediaDbContext _db;

    public MediaAssetRepository(MediaDbContext db) => _db = db;

    public async Task AddAsync(MediaAsset asset, CancellationToken ct = default)
    {
        await _db.MediaAssets.AddAsync(asset, ct);
    }

    public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.MediaAssets.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<MediaAsset> Items, int Total)> ListAsync(
        int page, int size, string? entityType, Guid? uploadedBy, CancellationToken ct = default)
    {
        var query = _db.MediaAssets.AsNoTracking().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType);

        if (uploadedBy.HasValue)
            query = query.Where(x => x.UploadedByUserId == uploadedBy.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.UploadedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
