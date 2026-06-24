using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Store.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly StoreDbContext _db;

    public ProductRepository(StoreDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => QueryWithDetails()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

    public Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => QueryWithDetails()
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted, ct);

    public async Task<(List<Product> Items, int Total)> GetPagedAsync(
        Guid? shopId, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Product> q;

        if (shopId.HasValue)
        {
            q = _db.ShopProducts
                .Where(sp => sp.ShopId == shopId.Value && sp.IsActive && !sp.IsDeleted)
                .Join(_db.Products,
                    sp => sp.ProductId,
                    p => p.Id,
                    (_, p) => p)
                .Where(p => !p.IsDeleted && p.IsAvailable);
        }
        else
        {
            q = _db.Products.Where(p => !p.IsDeleted && p.IsAvailable);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<Product> Items, int Total)> SearchAsync(
        string query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Products
            .Where(p => !p.IsDeleted && p.IsAvailable &&
                        (p.Title.Contains(query) || (p.Description != null && p.Description.Contains(query))));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<Product> Items, int Total)> SearchAsync(
        string query, IReadOnlyList<Guid> allowedAgreementProductIds,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Products.Where(p =>
            !p.IsDeleted && p.IsAvailable
            && allowedAgreementProductIds.Contains(p.AgreementProductId)
            && (p.Title.Contains(query) || (p.Description != null && p.Description.Contains(query))));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Product>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await _db.Products
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Images)
            .ToListAsync(ct);
    }

    public async Task<List<Product>> GetByIdsForAdminWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await QueryWithDetails()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Products.AnyAsync(p => p.Slug == slug, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _db.Products.AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddVariantAttributeAsync(
        Product product,
        VariantAttribute attribute,
        CancellationToken ct = default)
    {
        await ThrowIfVariantAttributeAlreadyExistsAsync(attribute.ProductId, attribute.Name, ct);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO store.variant_attributes (""Id"", ""ProductId"", ""Name"", ""SortOrder"")
            VALUES ({attribute.Id}, {attribute.ProductId}, {attribute.Name}, {attribute.SortOrder})", ct);
    }

    public async Task AddVariantAttributeValueAsync(
        Product product,
        VariantAttributeValue value,
        CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO store.variant_attribute_values (""Id"", ""VariantAttributeId"", ""Value"", ""SortOrder"")
            VALUES ({value.Id}, {value.VariantAttributeId}, {value.Value}, {value.SortOrder})", ct);
    }

    public async Task AddProductVariantAsync(
        Product product,
        ProductVariant variant,
        CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO store.product_variants
                (""Id"", ""ProductId"", ""SKU"", ""ImageUrl"", ""StockCount"", ""PriceMinor"", ""DiscountedPriceMinor"", ""FromDate"", ""ToDate"", ""CapacityType"", ""Capacity"", ""IsAvailable"")
            VALUES
                ({variant.Id}, {variant.ProductId}, {variant.SKU}, {variant.ImageUrl}, {variant.StockCount}, {variant.PriceMinor}, {variant.DiscountedPriceMinor}, {variant.FromDate}, {variant.ToDate}, {(short)variant.CapacityType}, {variant.Capacity}, {variant.IsAvailable})", ct);

        foreach (var combination in variant.Combinations)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO store.product_variant_combinations
                    (""Id"", ""ProductVariantId"", ""VariantAttributeId"", ""VariantAttributeValueId"")
                VALUES
                    ({combination.Id}, {combination.ProductVariantId}, {combination.VariantAttributeId}, {combination.VariantAttributeValueId})", ct);
        }

        await transaction.CommitAsync(ct);
    }
    public Task<Product?> GetByIdForAdminAsync(Guid id, CancellationToken ct = default)
        => QueryWithDetails()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(List<Product> Items, int Total)> GetPagedAdminAsync(
        Guid? shopId, bool? isDeleted,
        int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Product> q;

        if (shopId.HasValue)
        {
            q = _db.ShopProducts
                .Where(sp => sp.ShopId == shopId.Value && !sp.IsDeleted)
                .Join(_db.Products,
                    sp => sp.ProductId,
                    p => p.Id,
                    (_, p) => p);
        }
        else
        {
            q = _db.Products.AsQueryable();
        }

        if (isDeleted.HasValue)
            q = q.Where(p => p.IsDeleted == isDeleted.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        if (_db.Entry(product).State == EntityState.Detached)
            _db.Products.Update(product);

        SuppressTimestampOnlyProductUpdateWhenAddingChildren(product);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!await TrySuppressTimestampOnlyProductConcurrencyAsync(ex, ct))
                throw new StoreConcurrencyException();

            await _db.SaveChangesAsync(ct);
        }
    }

    private IQueryable<Product> QueryWithDetails()
        => _db.Products
            .AsSplitQuery()
            .Include(p => p.Images)
            .Include(p => p.Variants)
                .ThenInclude(v => v.Combinations)
            .Include(p => p.VariantAttributes)
                .ThenInclude(a => a.Values)
            .Include(p => p.Specifications)
            .Include(p => p.Sessions);

    private void SuppressTimestampOnlyProductUpdateWhenAddingChildren(Product product)
    {
        _db.ChangeTracker.DetectChanges();

        var productEntry = _db.Entry(product);

        if (productEntry.State != EntityState.Modified)
            return;

        if (!HasPendingProductChildAdditions(product.Id))
            return;

        productEntry.State = EntityState.Unchanged;
    }

    private async Task<bool> TrySuppressTimestampOnlyProductConcurrencyAsync(
        DbUpdateConcurrencyException exception,
        CancellationToken ct)
    {
        var productEntries = exception.Entries
            .Where(e => e.Entity is Product)
            .ToList();

        if (productEntries.Count == 0 || productEntries.Count != exception.Entries.Count)
            return false;

        _db.ChangeTracker.DetectChanges();

        if (!HasAnyPendingProductChildAdditions(productEntries))
            return false;

        if (!await AreProductsStillActiveAsync(productEntries, ct))
            return false;

        await ThrowIfAddedVariantAttributeAlreadyExistsAsync(ct);

        foreach (var entry in productEntries)
        {
            entry.Property(nameof(Product.UpdatedAt)).IsModified = false;
            entry.State = EntityState.Unchanged;
        }

        return true;
    }

    private bool HasAnyPendingProductChildAdditions(IEnumerable<EntityEntry> productEntries)
        => productEntries
            .Select(e => ((Product)e.Entity).Id)
            .Distinct()
            .Any(HasPendingProductChildAdditions);

    private bool HasPendingProductChildAdditions(Guid productId)
        => _db.ChangeTracker.Entries<VariantAttribute>()
               .Any(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
           || _db.ChangeTracker.Entries<VariantAttributeValue>()
               .Any(e => e.State == EntityState.Added)
           || _db.ChangeTracker.Entries<ProductVariant>()
               .Any(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
           || _db.ChangeTracker.Entries<ProductImage>()
               .Any(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
           || _db.ChangeTracker.Entries<ProductSpecification>()
               .Any(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
           || _db.ChangeTracker.Entries<ProductSession>()
               .Any(e => e.State == EntityState.Added && e.Entity.ProductId == productId);

    private async Task<bool> AreProductsStillActiveAsync(
        IReadOnlyCollection<EntityEntry> productEntries,
        CancellationToken ct)
    {
        var productIds = productEntries
            .Select(e => ((Product)e.Entity).Id)
            .Distinct()
            .ToList();

        var activeProductCount = await _db.Products
            .AsNoTracking()
            .CountAsync(p => productIds.Contains(p.Id) && !p.IsDeleted, ct);

        return activeProductCount == productIds.Count;
    }

    private async Task ThrowIfAddedVariantAttributeAlreadyExistsAsync(CancellationToken ct)
    {
        var addedAttributes = _db.ChangeTracker
            .Entries<VariantAttribute>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        foreach (var attribute in addedAttributes)
            await ThrowIfVariantAttributeAlreadyExistsAsync(attribute.ProductId, attribute.Name, ct);
    }

    private async Task ThrowIfVariantAttributeAlreadyExistsAsync(
        Guid productId,
        string name,
        CancellationToken ct)
    {
        var normalizedName = name.Trim();
        var normalizedNameLower = normalizedName.ToLower();
        var alreadyExists = await _db.VariantAttributes
            .AsNoTracking()
            .AnyAsync(a => a.ProductId == productId && a.Name.ToLower() == normalizedNameLower, ct);

        if (alreadyExists)
            throw new StoreDomainException(
                "ویژگی تنوع قبلاً برای این محصول ثبت شده است",
                "VARIANT_ATTRIBUTE_ALREADY_EXISTS");
    }

    private static bool IsOnlyUpdatedAtModified(EntityEntry entry)
    {
        var modifiedProperties = entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name)
            .ToList();

        return modifiedProperties.Count == 1
            && modifiedProperties[0] == nameof(Product.UpdatedAt);
    }
}
