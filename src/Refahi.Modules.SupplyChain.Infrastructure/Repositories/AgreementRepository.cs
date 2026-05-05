using Microsoft.EntityFrameworkCore;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Entities;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;

namespace Refahi.Modules.SupplyChain.Infrastructure.Repositories;

public class AgreementRepository : IAgreementRepository
{
    private readonly SupplyChainDbContext _context;

    public AgreementRepository(SupplyChainDbContext context)
        => _context = context;

    public async Task<Agreement?> GetByIdAsync(Guid id, bool includeProducts, CancellationToken ct)
    {
        var query = _context.Agreements
            .Include(a => a.Supplier)
            .AsQueryable();

        if (includeProducts)
            query = query.Include(a => a.Products);

        return await query.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Agreement> Items, int Total)> GetPagedAsync(
        Guid? supplierId, AgreementStatus? status, AgreementType? type, string? search,
        int page, int size, CancellationToken ct)
    {
        var query = _context.Agreements
            .Include(a => a.Supplier)
            .Where(a => !a.IsDeleted)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(a => a.SupplierId == supplierId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (type.HasValue)
            query = query.Where(a => a.AgreementType == type.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(a =>
                a.AgreementNo.ToLower().Contains(lower) ||
                (a.Supplier != null && (
                    (a.Supplier.CompanyName != null && a.Supplier.CompanyName.ToLower().Contains(lower)) ||
                    (a.Supplier.BrandName != null && a.Supplier.BrandName.ToLower().Contains(lower)) ||
                    (a.Supplier.FirstName != null && a.Supplier.FirstName.ToLower().Contains(lower)) ||
                    (a.Supplier.LastName != null && a.Supplier.LastName.ToLower().Contains(lower)))));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> ExistsByAgreementNoAsync(string agreementNo, Guid? excludeId, CancellationToken ct)
    {
        var query = _context.Agreements
            .Where(a => a.AgreementNo == agreementNo && !a.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }

    public Task<AgreementProduct?> GetProductByIdAsync(Guid productId, CancellationToken ct)
        => _context.AgreementProducts
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

    public async Task<IReadOnlyList<Guid>> GetApprovedProductIdsByCategoryAsync(
        int categoryId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await _context.AgreementProducts
            .Where(ap => ap.CategoryId == categoryId && !ap.IsDeleted)
            .Join(
                _context.Agreements.Where(a =>
                    a.Status == AgreementStatus.Approved &&
                    //a.ToDate >= now &&
                    !a.IsDeleted),
                ap => ap.AgreementId,
                a => a.Id,
                (ap, _) => ap.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDisplayableProductIdsByCategoriesAsync(
        IReadOnlyList<int> categoryIds, CancellationToken ct)
    {
        if (categoryIds.Count == 0)
            return [];

        var now = DateTimeOffset.UtcNow;
        return await (
            from ap in _context.AgreementProducts
            where !ap.IsDeleted
                  && ap.CategoryId != null
                  && categoryIds.Contains(ap.CategoryId.Value)
            join a in _context.Agreements
                          .Where(a => !a.IsDeleted
                                      && a.Status == AgreementStatus.Approved
                                      && a.ToDate >= now)
                on ap.AgreementId equals a.Id
            join s in _context.Suppliers
                          .Where(s => !s.IsDeleted && s.Status == SupplierStatus.Approved)
                on a.SupplierId equals s.Id
            select ap.Id)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, AgreementProductDto>> GetProductsByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, AgreementProductDto>();

        return await _context.AgreementProducts
            .Where(ap => ids.Contains(ap.Id) && !ap.IsDeleted)
            .ToDictionaryAsync(
                ap => ap.Id,
                ap => new AgreementProductDto(
                    ap.Id, ap.AgreementId, ap.Name, ap.Description, ap.CategoryId,
                    null,
                    (short)ap.ProductType, (short)ap.DeliveryType, (short)ap.SalesModel,
                    ap.CommissionPercent, ap.IsDeleted, ap.CreatedAt),
                ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetCommissionPercentsByIdsAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, decimal>();

        return await _context.AgreementProducts
            .Where(ap => ids.Contains(ap.Id) && !ap.IsDeleted)
            .ToDictionaryAsync(ap => ap.Id, ap => ap.CommissionPercent, ct);
    }

    public async Task AddAsync(Agreement agreement, CancellationToken ct)
        => await _context.Agreements.AddAsync(agreement, ct);

    public void Update(Agreement agreement)
        => _context.Agreements.Update(agreement);

    public void AddProduct(AgreementProduct product)
        => _context.AgreementProducts.Add(product);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await _context.SaveChangesAsync(ct);
}
