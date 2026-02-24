using Refahi.Modules.Identity.Domain.Aggregates;

namespace Refahi.Modules.Identity.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByMobileOrEmailAsync(string mobileOrEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByMobileOrEmailOrUsernameAsync(string loginIdentifier, CancellationToken cancellationToken = default);
    Task<bool> ExistsByMobileNumberAsync(string mobileNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
