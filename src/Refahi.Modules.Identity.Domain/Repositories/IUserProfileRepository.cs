using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserProfile profile, CancellationToken cancellationToken = default);
}
