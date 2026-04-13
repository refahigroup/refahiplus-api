using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Orders.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public OrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    public async Task<Order?> GetByIdWithItemsAsync(Guid orderId, CancellationToken ct = default)
    {
        return await _context.Orders
            .Include("_items")
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<List<Order>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.Orders
            .Include("_items")
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Orders
            .CountAsync(o => o.UserId == userId, ct);
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(ct);
    }
}
