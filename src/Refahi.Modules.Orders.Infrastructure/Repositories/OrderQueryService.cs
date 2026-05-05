using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Domain.Enums;
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
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _context.Orders.Where(o => o.UserId == userId);

        if (statuses?.Length > 0)
            q = q.Where(o => statuses.Contains(o.Status));

        if (!string.IsNullOrWhiteSpace(sourceModule))
            q = q.Where(o => o.SourceModule == sourceModule);

        return await q
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

    public async Task<int> CountUserOrdersAsync(
        Guid userId,
        OrderStatus[]? statuses,
        string? sourceModule,
        CancellationToken ct = default)
    {
        var q = _context.Orders.Where(o => o.UserId == userId);

        if (statuses?.Length > 0)
            q = q.Where(o => statuses.Contains(o.Status));

        if (!string.IsNullOrWhiteSpace(sourceModule))
            q = q.Where(o => o.SourceModule == sourceModule);

        return await q.CountAsync(ct);
    }
}
