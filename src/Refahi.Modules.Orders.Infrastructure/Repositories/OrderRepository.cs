using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
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
            .Include(o => o.Items)
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
            .Include(o => o.Items)
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

    public async Task<List<Order>> GetAllAsync(int page, int pageSize, string? status, Guid? userId, string? sourceModule, CancellationToken ct = default)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(sourceModule))
            query = query.Where(o => o.SourceModule == sourceModule);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAllAsync(string? status, Guid? userId, string? sourceModule, CancellationToken ct = default)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(sourceModule))
            query = query.Where(o => o.SourceModule == sourceModule);

        return await query.CountAsync(ct);
    }

    public async Task<List<Order>> GetBySourceAsync(string sourceModule, Guid sourceReferenceId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.Orders
            .Where(o => o.SourceModule == sourceModule && o.SourceReferenceId == sourceReferenceId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountBySourceAsync(string sourceModule, Guid sourceReferenceId, CancellationToken ct = default)
    {
        return await _context.Orders
            .CountAsync(o => o.SourceModule == sourceModule && o.SourceReferenceId == sourceReferenceId, ct);
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
