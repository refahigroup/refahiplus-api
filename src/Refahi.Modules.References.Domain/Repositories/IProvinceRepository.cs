using Refahi.Modules.References.Domain.Entities;

namespace Refahi.Modules.References.Domain.Repositories;

public interface IProvinceRepository
{
    Task<Province?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Province?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Province>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(Province province, CancellationToken ct = default);
    Task UpdateAsync(Province province, CancellationToken ct = default);
}
