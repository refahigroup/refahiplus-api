using Microsoft.EntityFrameworkCore;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;
using Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;

namespace Refahi.Modules.SupplyChain.Infrastructure.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly SupplyChainDbContext _context;

    public SupplierRepository(SupplyChainDbContext context)
        => _context = context;

    public async Task<Supplier?> GetByIdAsync(Guid id, bool includeChildren, CancellationToken ct)
    {
        var query = _context.Suppliers.AsQueryable();

        if (includeChildren)
        {
            query = query
                .Include(s => s.Links)
                .Include(s => s.Attachments);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Supplier> Items, int Total)> GetPagedAsync(
        SupplierStatus? status, SupplierType? type, int? provinceId, string? search,
        int page, int size, CancellationToken ct)
    {
        var query = _context.Suppliers
            .Where(s => !s.IsDeleted)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        if (provinceId.HasValue)
            query = query.Where(s => s.ProvinceId == provinceId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(lower)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(lower)) ||
                (s.CompanyName != null && s.CompanyName.ToLower().Contains(lower)) ||
                (s.BrandName != null && s.BrandName.ToLower().Contains(lower)) ||
                (s.NationalId != null && s.NationalId.Contains(search)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> ExistsByNationalIdAsync(string nationalId, Guid? excludeId, CancellationToken ct)
    {
        var query = _context.Suppliers
            .Where(s => s.NationalId == nationalId && !s.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct)
        => await _context.Suppliers.AddAsync(supplier, ct);

    public void Update(Supplier supplier)
        => _context.Suppliers.Update(supplier);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await _context.SaveChangesAsync(ct);
}
