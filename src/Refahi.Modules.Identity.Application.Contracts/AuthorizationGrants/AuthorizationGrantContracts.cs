using MediatR;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;

public sealed record AuthorizationGrantDto(
    Guid Id, Guid UserId, string Issuer, string Value, string? EmittedRole,
    bool IsActive, DateTimeOffset CreatedAt, Guid? CreatedBy,
    DateTimeOffset? RevokedAt, Guid? RevokedBy);

public sealed record UpsertAuthorizationGrantCommand(
    Guid UserId, string Issuer, string Value, string? EmittedRole, Guid? ActorId)
    : IRequest<AuthorizationGrantDto>;

public sealed record RevokeAuthorizationGrantCommand(Guid GrantId, Guid? ActorId) : IRequest<bool>;

public sealed record GetActiveAuthorizationGrantsQuery(Guid UserId, string? Issuer = null)
    : IRequest<IReadOnlyList<AuthorizationGrantDto>>;

public sealed record GetAuthorizationGrantsByIssuerQuery(string Issuer)
    : IRequest<IReadOnlyList<AuthorizationGrantDto>>;
