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

public sealed class AuthorizationGrantRepository(IdentityDbContext db)
    : IAuthorizationGrantRepository
{
    public Task<AuthorizationGrant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.AuthorizationGrants.SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<AuthorizationGrant?> GetAsync(
        Guid userId,
        string issuer,
        string value,
        CancellationToken ct = default
    ) =>
        db.AuthorizationGrants.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Issuer == issuer && x.Value == value,
            ct
        );

    public async Task<IReadOnlyList<AuthorizationGrant>> GetActiveAsync(
        Guid userId,
        string? issuer = null,
        CancellationToken ct = default
    ) =>
        await db
            .AuthorizationGrants.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && (issuer == null || x.Issuer == issuer))
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuthorizationGrant>> GetByIssuerAsync(
        string issuer,
        CancellationToken ct = default
    ) =>
        await db
            .AuthorizationGrants.AsNoTracking()
            .Where(x => x.Issuer == issuer)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(AuthorizationGrant grant, CancellationToken ct = default) =>
        await db.AuthorizationGrants.AddAsync(grant, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
