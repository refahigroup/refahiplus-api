using Identity.Domain.Aggregates;

namespace Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<UserAggregate> GetByUsername(string username);
    Task<UserAggregate> GetByIdAsync(string id);
}
