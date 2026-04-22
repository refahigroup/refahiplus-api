using Refahi.Modules.References.Domain.Entities;

namespace Refahi.Modules.References.Domain.Repositories;

public interface ICityRepository
{
    Task<City?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<City?> GetBySlugAsync(int provinceId, string slug, CancellationToken ct = default);
    Task<List<City>> GetAllAsync(int? provinceId = null, bool activeOnly = false, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, int provinceId, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(City city, CancellationToken ct = default);
    Task UpdateAsync(City city, CancellationToken ct = default);
}
