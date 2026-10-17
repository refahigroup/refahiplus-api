using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Purchases;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Get;
public sealed class GetPoolPurchaseHandler(PoolPurchaseAccess access) : IRequestHandler<GetPoolPurchaseQuery, PoolPurchaseDto>
{
    public async Task<PoolPurchaseDto> Handle(GetPoolPurchaseQuery r, CancellationToken ct)
    { return access.View(await access.Owned(r.Id, r.UserId, ct)); }
}
