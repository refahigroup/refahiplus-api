using Refahi.Modules.Orders.Application.Contracts.Dtos;

namespace Refahi.Modules.Orders.Application.Contracts.Repositories;

public interface IOrderQueryService
{
    Task<List<OrderSummaryDto>> GetUserOrderSummariesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
}
