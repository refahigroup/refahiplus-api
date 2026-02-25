using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<User?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.MobileNumber == mobileNumber, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower(), cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == username.ToLower(), cancellationToken);
    }

    public async Task<User?> GetByMobileOrEmailAsync(string mobileOrEmail, CancellationToken cancellationToken = default)
    {
        mobileOrEmail = mobileOrEmail.Trim();
        
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => 
                u.MobileNumber == mobileOrEmail || 
                u.Email == mobileOrEmail.ToLower(), 
                cancellationToken);
    }

    public async Task<User?> GetByMobileOrEmailOrUsernameAsync(string loginIdentifier, CancellationToken cancellationToken = default)
    {
        loginIdentifier = loginIdentifier.Trim();
        var lowerIdentifier = loginIdentifier.ToLower();
        
        return await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => 
                u.MobileNumber == loginIdentifier || 
                u.Email == lowerIdentifier ||
                u.Username == lowerIdentifier, 
                cancellationToken);
    }

    public async Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default)
    {
        return await _db.Users.AnyAsync(u => u.MobileNumber == mobileNumber, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Users.AnyAsync(u => u.Email == email.ToLower(), cancellationToken);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _db.Users.AnyAsync(u => u.Username == username.ToLower(), cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
