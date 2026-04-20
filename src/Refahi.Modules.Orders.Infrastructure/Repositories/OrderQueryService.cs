using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Orders.Infrastructure.Repositories;

public class OrderQueryService : IOrderQueryService
{
    private readonly OrdersDbContext _context;

    public OrderQueryService(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<List<OrderSummaryDto>> GetUserOrderSummariesAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.FinalAmountMinor,
                o.Status.ToString(),
                o.SourceModule,
                o.Items.Count,
                o.CreatedAt))
            .ToListAsync(ct);
    }
}
