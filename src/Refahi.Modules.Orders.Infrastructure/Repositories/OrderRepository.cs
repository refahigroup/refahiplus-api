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
            .Include(o => o.PaymentPostings)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.Orders
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<Order?> GetByIdempotencyKeyWithItemsAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.PaymentPostings)
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

    public async Task<List<Order>> GetAllAsync(
        int page,
        int pageSize,
        string? status,
        Guid? userId,
        string? sourceModule,
        IReadOnlyCollection<Guid>? allowedUserIds = null,
        CancellationToken ct = default)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(sourceModule))
            query = query.Where(o => o.SourceModule == sourceModule);

        if (allowedUserIds is not null)
        {
            if (allowedUserIds.Count == 0)
                return [];

            query = query.Where(o => allowedUserIds.Contains(o.UserId));
        }

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAllAsync(
        string? status,
        Guid? userId,
        string? sourceModule,
        IReadOnlyCollection<Guid>? allowedUserIds = null,
        CancellationToken ct = default)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(sourceModule))
            query = query.Where(o => o.SourceModule == sourceModule);

        if (allowedUserIds is not null)
        {
            if (allowedUserIds.Count == 0)
                return 0;

            query = query.Where(o => allowedUserIds.Contains(o.UserId));
        }

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

    public async Task<(List<Order> Orders, int Total)> GetVendorOrdersAsync(
        IReadOnlyCollection<Guid> supplierIds, IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds, Guid actorUserId, int page, int pageSize,
        string? status, string? paymentState, string? orderNumber,
        IReadOnlyCollection<Guid>? userIds, Guid? shopId,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = _context.Orders.AsNoTracking()
            .Where(x => x.SourceModule == "Store" && (
                (x.SourceOwnerId.HasValue && supplierIds.Contains(x.SourceOwnerId.Value)) ||
                (x.SourceShopId.HasValue && shopIds.Contains(x.SourceShopId.Value)) ||
                (x.SourceShopId.HasValue && ownShopIds.Contains(x.SourceShopId.Value) &&
                    x.CreatedByUserId == actorUserId)));
        if (Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            query = query.Where(x => x.Status == parsedStatus);
        if (Enum.TryParse<PaymentState>(paymentState, true, out var parsedPayment))
            query = query.Where(x => x.PaymentState == parsedPayment);
        if (!string.IsNullOrWhiteSpace(orderNumber))
            query = query.Where(x => x.OrderNumber.Contains(orderNumber.Trim()));
        if (userIds is not null)
        {
            if (userIds.Count == 0) return ([], 0);
            query = query.Where(x => userIds.Contains(x.UserId));
        }
        if (shopId.HasValue) query = query.Where(x => x.SourceShopId == shopId.Value);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedAt <= to.Value);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var orders = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (orders, total);
    }

    public Task<Order?> GetVendorOrderByIdAsync(
        Guid orderId, IReadOnlyCollection<Guid> supplierIds, IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds, Guid actorUserId, CancellationToken ct = default)
        => _context.Orders.Include(x => x.Items).FirstOrDefaultAsync(
            x => x.Id == orderId && x.SourceModule == "Store" &&
                 ((x.SourceOwnerId.HasValue && supplierIds.Contains(x.SourceOwnerId.Value)) ||
                  (x.SourceShopId.HasValue && shopIds.Contains(x.SourceShopId.Value)) ||
                  (x.SourceShopId.HasValue && ownShopIds.Contains(x.SourceShopId.Value) &&
                   x.CreatedByUserId == actorUserId)), ct);

    public async Task<(int Pending, int Processing)> GetVendorStatusCountsAsync(
        IReadOnlyCollection<Guid> supplierIds, IReadOnlyCollection<Guid> shopIds,
        IReadOnlyCollection<Guid> ownShopIds, Guid actorUserId, CancellationToken ct = default)
    {
        var query = _context.Orders.AsNoTracking().Where(x =>
            x.SourceModule == "Store" && (
                (x.SourceOwnerId.HasValue && supplierIds.Contains(x.SourceOwnerId.Value)) ||
                (x.SourceShopId.HasValue && shopIds.Contains(x.SourceShopId.Value)) ||
                (x.SourceShopId.HasValue && ownShopIds.Contains(x.SourceShopId.Value) &&
                 x.CreatedByUserId == actorUserId)));
        return (
            await query.CountAsync(x =>
                x.Status == OrderStatus.Pending || x.Status == OrderStatus.Confirmed, ct),
            await query.CountAsync(x => x.Status == OrderStatus.Processing, ct));
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
