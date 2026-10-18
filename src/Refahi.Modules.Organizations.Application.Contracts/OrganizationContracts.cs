using MediatR;
namespace Refahi.Modules.Organizations.Application.Contracts;

public sealed record OrganizationDto(Guid Id, string Code, string Title, bool IsActive);
public sealed record MembershipDto(Guid Id, Guid OrganizationId, Guid UserId, DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil, DateTimeOffset? RevokedAt);
public sealed record GetActiveUserOrganizationsQuery(Guid UserId) : IRequest<Guid[]>;
public sealed record ListOrganizationsQuery : IRequest<List<OrganizationDto>>;
public sealed record SaveOrganizationCommand(Guid? Id, string Code, string Title, bool IsActive = true, Guid ActorId = default) : IRequest<OrganizationDto>;
public sealed record ListOrganizationMembersQuery(Guid OrganizationId) : IRequest<List<MembershipDto>>;
public sealed record AddOrganizationMemberCommand(Guid OrganizationId, Guid UserId, Guid ActorId,
    DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil) : IRequest<MembershipDto>;
public sealed record RevokeOrganizationMemberCommand(Guid OrganizationId, Guid MembershipId, Guid ActorId) : IRequest;
