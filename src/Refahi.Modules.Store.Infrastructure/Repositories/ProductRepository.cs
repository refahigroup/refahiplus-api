using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public sealed class ProductRepository(StoreDbContext db) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        QueryWithDetails().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default) =>
        QueryWithDetails().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) =>
        db.Products.AnyAsync(x => x.Slug == slug, ct);

    public async Task<(List<Product> Items, int Total)> GetCatalogPagedAsync(
        Guid? supplierId,
        int? categoryId,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = db.Products.AsNoTracking().Where(x => !x.IsDeleted);
        if (!includeInactive)
            query = query.Where(x => x.IsAvailable);
        if (supplierId.HasValue)
            query = query.Where(x => x.SupplierId == supplierId.Value);
        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<Product>> GetCatalogEligibilityCandidatesAsync(
        Guid? supplierId,
        int? categoryId,
        CancellationToken ct = default
    )
    {
        var query = db.Products.AsNoTracking().Where(x => !x.IsDeleted && x.IsAvailable);
        if (supplierId.HasValue)
            query = query.Where(x => x.SupplierId == supplierId.Value);
        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId.Value);
        return query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<(List<Product> Items, int Total)> GetCatalogPageByIdsAsync(
        IReadOnlyCollection<Guid> eligibleIds,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        if (eligibleIds.Count == 0)
            return ([], 0);
        var query = db.Products.AsNoTracking().Where(x => eligibleIds.Contains(x.Id));
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddVariantAttributeAsync(
        Product product,
        VariantAttribute attribute,
        CancellationToken ct = default
    )
    {
        var normalizedName = attribute.Name.Trim().ToLower();
        if (await db.VariantAttributes.AsNoTracking().AnyAsync(
                x => x.ProductId == attribute.ProductId && x.Name.ToLower() == normalizedName,
                ct))
            throw new StoreDomainException(
                "ویژگی تنوع قبلاً برای این محصول ثبت شده است",
                "VARIANT_ATTRIBUTE_ALREADY_EXISTS"
            );

        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO store.variant_attributes (""Id"", ""ProductId"", ""Name"", ""SortOrder"")
               VALUES ({attribute.Id}, {attribute.ProductId}, {attribute.Name}, {attribute.SortOrder})",
            ct
        );
    }

    public Task AddVariantAttributeValueAsync(
        Product product,
        VariantAttributeValue value,
        CancellationToken ct = default
    ) => db.Database.ExecuteSqlInterpolatedAsync(
        $@"INSERT INTO store.variant_attribute_values (""Id"", ""VariantAttributeId"", ""Value"", ""SortOrder"")
           VALUES ({value.Id}, {value.VariantAttributeId}, {value.Value}, {value.SortOrder})",
        ct
    );

    public async Task AddProductVariantAsync(
        Product product,
        ProductVariant variant,
        CancellationToken ct = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO store.product_variants
               (""Id"", ""ProductId"", ""SKU"", ""ImageUrl"", ""StockCount"", ""FromDate"", ""ToDate"", ""CapacityType"", ""Capacity"", ""IsAvailable"")
               VALUES ({variant.Id}, {variant.ProductId}, {variant.SKU}, {variant.ImageUrl}, {variant.StockCount},
                       {variant.FromDate}, {variant.ToDate}, {(short)variant.CapacityType}, {variant.Capacity}, {variant.IsAvailable})",
            ct
        );
        foreach (var combination in variant.Combinations)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO store.product_variant_combinations
                   (""Id"", ""ProductVariantId"", ""VariantAttributeId"", ""VariantAttributeValueId"")
                   VALUES ({combination.Id}, {combination.ProductVariantId},
                           {combination.VariantAttributeId}, {combination.VariantAttributeValueId})",
                ct
            );
        await transaction.CommitAsync(ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        if (db.Entry(product).State == EntityState.Detached)
            db.Products.Update(product);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StoreConcurrencyException();
        }
    }

    private IQueryable<Product> QueryWithDetails() =>
        db.Products
            .AsSplitQuery()
            .Include(x => x.Images)
            .Include(x => x.Variants).ThenInclude(x => x.Combinations)
            .Include(x => x.VariantAttributes).ThenInclude(x => x.Values)
            .Include(x => x.Specifications)
            .Include(x => x.Sessions);
}
