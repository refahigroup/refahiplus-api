using MediatR;
using Refahi.Modules.Identity.Application.Contracts.AuthorizationGrants;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operators;
public sealed class PoolOperatorAccess(IMediator mediator)
{
    public static string Value(Guid poolId) => $"pool:{poolId:N}:redeem";
    public static Guid? Parse(string value) => value.StartsWith("pool:", StringComparison.Ordinal) && value.EndsWith(":redeem", StringComparison.Ordinal)
        && Guid.TryParseExact(value[5..^7], "N", out var id) ? id : null;
    public Task<IReadOnlyList<AuthorizationGrantDto>> Grants(Guid user, CancellationToken ct) => mediator.Send(new GetActiveAuthorizationGrantsQuery(user, PoolKeys.GrantIssuer), ct);
    public async Task Ensure(Guid user, Guid pool, CancellationToken ct)
    {
        if (!(await Grants(user, ct)).Any(x => x.IsActive && Parse(x.Value) == pool))
            throw new PoolException("دسترسی پذیرش این استخر را ندارید", 403, "REDEMPTION_FORBIDDEN");
    }
}
