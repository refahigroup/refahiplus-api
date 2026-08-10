using Refahi.Modules.Identity.Domain.Entities;

namespace Refahi.Modules.Identity.Domain.Repositories;

public interface IAuthorizationGrantRepository
{
    Task<AuthorizationGrant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AuthorizationGrant?> GetAsync(
        Guid userId,
        string issuer,
        string value,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<AuthorizationGrant>> GetActiveAsync(
        Guid userId,
        string? issuer = null,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<AuthorizationGrant>> GetByIssuerAsync(
        string issuer,
        CancellationToken ct = default
    );
    Task AddAsync(AuthorizationGrant grant, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
