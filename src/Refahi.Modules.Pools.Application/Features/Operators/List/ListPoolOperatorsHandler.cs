using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators.List;
public sealed class ListPoolOperatorsHandler(IMediator mediator) : IRequestHandler<ListPoolOperatorsQuery, IReadOnlyList<PoolOperatorGrantDto>>
{
    public async Task<IReadOnlyList<PoolOperatorGrantDto>> Handle(ListPoolOperatorsQuery r, CancellationToken ct) =>
        (await mediator.Send(new GetAuthorizationGrantsByIssuerQuery(PoolKeys.GrantIssuer), ct))
            .Where(x => x.IsActive && PoolOperatorAccess.Parse(x.Value).HasValue).Select(x => new PoolOperatorGrantDto(x.Id, x.UserId, PoolOperatorAccess.Parse(x.Value)!.Value)).ToArray();
}
