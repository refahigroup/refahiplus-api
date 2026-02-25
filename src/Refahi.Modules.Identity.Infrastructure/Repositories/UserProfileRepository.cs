using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Identity.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly IdentityDbContext _db;

    public UserProfileRepository(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserProfiles.AnyAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        await _db.UserProfiles.AddAsync(profile, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _db.UserProfiles.Update(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
