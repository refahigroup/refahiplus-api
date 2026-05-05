using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Identity.Infrastructure.Repositories;

public class UserAddressRepository : IUserAddressRepository
{
    private readonly IdentityDbContext _db;

    public UserAddressRepository(IdentityDbContext db)
    {
        _db = db;
    }

    public Task<UserAddress?> GetByIdAsync(Guid addressId, CancellationToken cancellationToken = default)
        => _db.UserAddresses.FirstOrDefaultAsync(a => a.Id == addressId, cancellationToken);

    public Task<UserAddress?> GetByIdForUserAsync(Guid addressId, Guid userId, CancellationToken cancellationToken = default)
        => _db.UserAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<UserAddress>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var list = await _db.UserAddresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return list;
    }

    public Task<UserAddress?> GetDefaultForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.UserAddresses.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

    public async Task AddAsync(UserAddress address, CancellationToken cancellationToken = default)
    {
        await _db.UserAddresses.AddAsync(address, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAddress address, CancellationToken cancellationToken = default)
    {
        _db.UserAddresses.Update(address);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(UserAddress address, CancellationToken cancellationToken = default)
    {
        _db.UserAddresses.Remove(address);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsetDefaultForUserAsync(Guid userId, Guid? exceptAddressId = null, CancellationToken cancellationToken = default)
    {
        var defaults = await _db.UserAddresses
            .Where(a => a.UserId == userId && a.IsDefault &&
                        (exceptAddressId == null || a.Id != exceptAddressId.Value))
            .ToListAsync(cancellationToken);

        if (defaults.Count == 0) return;

        foreach (var addr in defaults)
            addr.UnmarkAsDefault();

        _db.UserAddresses.UpdateRange(defaults);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
