using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
