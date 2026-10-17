using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Operators.Grant;
public sealed class GrantPoolOperatorHandler(IMediator mediator, IPoolCatalogRepository catalog) : IRequestHandler<GrantPoolOperatorCommand, PoolOperatorGrantDto>
{
    public async Task<PoolOperatorGrantDto> Handle(GrantPoolOperatorCommand r, CancellationToken ct)
    {
        if (r.UserId == Guid.Empty || r.ActorId == Guid.Empty || (await catalog.PoolAsync(r.PoolId, ct))?.IsActive != true)
            throw new PoolException("کاربر یا استخر معتبر نیست", 400);
        if (await mediator.Send(new GetUserCommerceContactQuery(r.UserId), ct) == null) throw new PoolException("کاربر یافت نشد", 404);
        var grant = await mediator.Send(new UpsertAuthorizationGrantCommand(r.UserId, PoolKeys.GrantIssuer, PoolOperatorAccess.Value(r.PoolId), null, r.ActorId), ct);
        return new(grant.Id, grant.UserId, r.PoolId);
    }
}
