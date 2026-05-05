using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Domain.Enums;

namespace Refahi.Modules.Orders.Application.Contracts.Repositories;

public interface IOrderQueryService
{
    Task<List<OrderSummaryDto>> GetUserOrderSummariesAsync(
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> CountUserOrdersAsync(
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        CancellationToken ct = default);
}
