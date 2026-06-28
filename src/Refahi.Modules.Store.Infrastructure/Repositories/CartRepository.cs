using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly StoreDbContext _db;

    public CartRepository(StoreDbContext db) => _db = db;

    public Task<Cart?> GetByUserAndModuleIdAsync(Guid userId, int moduleId, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ModuleId == moduleId, ct);

    public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public Task<Cart?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart> AddItemAsync(
        Guid userId,
        int moduleId,
        Guid shopId,
        Guid productId,
        Guid? variantId,
        Guid? sessionId,
        DateOnly? usageDate,
        int quantity,
        long unitPriceMinor,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            throw new StoreDomainException("تعداد باید بیشتر از صفر باشد", "INVALID_QUANTITY");
        if (unitPriceMinor <= 0)
            throw new StoreDomainException("قیمت واحد باید بیشتر از صفر باشد", "INVALID_PRICE");

        _db.ChangeTracker.Clear();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var lockKey = CreateCartLockKey(userId, moduleId);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})", ct);

        var cartId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO store.carts
                (""Id"", ""UserId"", ""ModuleId"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                ({cartId}, {userId}, {moduleId}, {now}, {now})
            ON CONFLICT (""UserId"", ""ModuleId"")
            DO UPDATE SET ""UpdatedAt"" = EXCLUDED.""UpdatedAt""", ct);

        cartId = await _db.Carts
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ModuleId == moduleId)
            .Select(c => c.Id)
            .SingleAsync(ct);

        var affectedRows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE store.cart_items
            SET
                ""Quantity"" = ""Quantity"" + {quantity}
            WHERE ""CartId"" = {cartId}
              AND ""ShopId"" = {shopId}
              AND ""ProductId"" = {productId}
              AND ""VariantId"" IS NOT DISTINCT FROM {variantId}
              AND ""SessionId"" IS NOT DISTINCT FROM {sessionId}
              AND ""UsageDate"" IS NOT DISTINCT FROM {usageDate}", ct);

        if (affectedRows == 0)
        {
            var itemId = Guid.NewGuid();
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO store.cart_items
                    (""Id"", ""CartId"", ""ShopId"", ""ProductId"", ""VariantId"", ""SessionId"", ""UsageDate"", ""Quantity"", ""UnitPriceMinor"")
                VALUES
                    ({itemId}, {cartId}, {shopId}, {productId}, {variantId}, {sessionId}, {usageDate}, {quantity}, {unitPriceMinor})", ct);
        }

        var cart = await _db.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .SingleAsync(c => c.Id == cartId, ct);

        await transaction.CommitAsync(ct);
        return cart;
    }
    public async Task AddAsync(Cart cart, CancellationToken ct = default)
    {
        await _db.Carts.AddAsync(cart, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Cart cart, CancellationToken ct = default)
    {
        // Cart aggregates are loaded tracked. Calling Update marks new GUID-keyed
        // cart items as Modified, which makes EF issue UPDATE instead of INSERT.
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Cart cart, CancellationToken ct = default)
    {
        _db.Carts.Remove(cart);
        await _db.SaveChangesAsync(ct);
    }

    private static long CreateCartLockKey(Guid userId, int moduleId)
    {
        Span<byte> bytes = stackalloc byte[16];
        userId.TryWriteBytes(bytes);

        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8])
            ^ BinaryPrimitives.ReadInt64LittleEndian(bytes[8..])
            ^ moduleId;
    }
}
