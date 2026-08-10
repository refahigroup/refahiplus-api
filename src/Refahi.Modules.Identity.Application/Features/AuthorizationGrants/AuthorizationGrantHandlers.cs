using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Domain.Repositories;

namespace Refahi.Modules.Identity.Application.Features.AuthorizationGrants;

public sealed class UpsertAuthorizationGrantHandler(
    IAuthorizationGrantRepository grants,
    IUserRepository users
) : IRequestHandler<UpsertAuthorizationGrantCommand, AuthorizationGrantDto>
{
    public async Task<AuthorizationGrantDto> Handle(
        UpsertAuthorizationGrantCommand request,
        CancellationToken ct
    )
    {
        if (await users.GetByIdAsync(request.UserId, ct) is null)
            throw new InvalidOperationException("کاربر یافت نشد");

        var grant = await grants.GetAsync(
            request.UserId,
            request.Issuer.Trim(),
            request.Value.Trim(),
            ct
        );
        if (grant is null)
        {
            grant = AuthorizationGrant.Create(
                request.UserId,
                request.Issuer,
                request.Value,
                request.EmittedRole,
                request.ActorId
            );
            await grants.AddAsync(grant, ct);
        }
        else
        {
            grant.Reactivate(request.EmittedRole, request.ActorId);
        }

        await grants.SaveChangesAsync(ct);
        return Map(grant);
    }

    internal static AuthorizationGrantDto Map(AuthorizationGrant x) =>
        new(
            x.Id,
            x.UserId,
            x.Issuer,
            x.Value,
            x.EmittedRole,
            x.IsActive,
            x.CreatedAt,
            x.CreatedBy,
            x.RevokedAt,
            x.RevokedBy
        );
}

public sealed class RevokeAuthorizationGrantHandler(IAuthorizationGrantRepository grants)
    : IRequestHandler<RevokeAuthorizationGrantCommand, bool>
{
    public async Task<bool> Handle(RevokeAuthorizationGrantCommand request, CancellationToken ct)
    {
        var grant = await grants.GetByIdAsync(request.GrantId, ct);
        if (grant is null)
            return false;
        grant.Revoke(request.ActorId);
        await grants.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class GetActiveAuthorizationGrantsHandler(IAuthorizationGrantRepository grants)
    : IRequestHandler<GetActiveAuthorizationGrantsQuery, IReadOnlyList<AuthorizationGrantDto>>
{
    public async Task<IReadOnlyList<AuthorizationGrantDto>> Handle(
        GetActiveAuthorizationGrantsQuery request,
        CancellationToken ct
    ) =>
        (await grants.GetActiveAsync(request.UserId, request.Issuer, ct))
            .Select(UpsertAuthorizationGrantHandler.Map)
            .ToList();
}

public sealed class GetAuthorizationGrantsByIssuerHandler(IAuthorizationGrantRepository grants)
    : IRequestHandler<GetAuthorizationGrantsByIssuerQuery, IReadOnlyList<AuthorizationGrantDto>>
{
    public async Task<IReadOnlyList<AuthorizationGrantDto>> Handle(
        GetAuthorizationGrantsByIssuerQuery request,
        CancellationToken ct
    ) =>
        (await grants.GetByIssuerAsync(request.Issuer.Trim(), ct))
            .Select(UpsertAuthorizationGrantHandler.Map)
            .ToList();
}
