using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.Revoke;
public sealed class RevokePoolOperatorHandler(IMediator mediator) : IRequestHandler<RevokePoolOperatorCommand, bool>
{
    public async Task<bool> Handle(RevokePoolOperatorCommand r, CancellationToken ct)
    {
        var grants = await mediator.Send(new GetAuthorizationGrantsByIssuerQuery(PoolKeys.GrantIssuer), ct);
        if (!grants.Any(x => x.Id == r.GrantId && x.IsActive)) throw new PoolException("دسترسی استخر یافت نشد", 404);
        return await mediator.Send(new RevokeAuthorizationGrantCommand(r.GrantId, r.ActorId), ct);
    }
}
