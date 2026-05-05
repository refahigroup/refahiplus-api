using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Domain.Repositories;

public interface IDailyDealRepository
{
    Task<DailyDeal?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<DailyDeal?> GetActiveByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<List<DailyDeal>> GetCurrentlyActiveAsync(CancellationToken ct = default);
    Task<List<DailyDeal>> GetCurrentlyActiveByModuleAsync(int moduleId, CancellationToken ct = default);
    Task<List<DailyDeal>> GetCurrentlyActiveByShopAsync(Guid shopId, CancellationToken ct = default);
    Task<List<DailyDeal>> GetAllAsync(int? moduleId = null, Guid? shopId = null, CancellationToken ct = default);
    Task AddAsync(DailyDeal deal, CancellationToken ct = default);
    Task UpdateAsync(DailyDeal deal, CancellationToken ct = default);
}
