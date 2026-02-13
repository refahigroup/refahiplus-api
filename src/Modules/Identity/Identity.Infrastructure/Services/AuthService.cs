using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Identity.Infrastructure.Services;

public class AuthService : IUserRepository
{
    private readonly IdentityDbContext _db;

    public AuthService(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<UserAggregate> GetByUsername(string username)
    {
        var userEntity = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (userEntity is null) 
            return null;

        return new UserAggregate(userEntity.Id, userEntity.Username, userEntity.PasswordHash, userEntity.Roles);

    }

    public async Task<UserAggregate> GetByIdAsync(string id)
    {
        var userEntity = await _db.Users.FindAsync(id);

        if (userEntity is null) 
            return null;

        return new UserAggregate(
            userEntity.Id, 
            userEntity.Username, 
            userEntity.PasswordHash, 
            userEntity.Roles
        );

    }
}
