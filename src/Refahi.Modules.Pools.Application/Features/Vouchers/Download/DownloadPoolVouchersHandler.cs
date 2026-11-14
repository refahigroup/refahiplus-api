using MediatR;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Purchases;
namespace Refahi.Modules.Pools.Application.Features.Vouchers.Download;
public sealed class DownloadPoolVouchersHandler(PoolPurchaseAccess access, IPoolVoucherRenderer renderer) : IRequestHandler<DownloadPoolVouchersQuery, byte[]>
{
    public async Task<byte[]> Handle(DownloadPoolVouchersQuery r, CancellationToken ct)
    { var p = access.View(await access.Owned(r.Id, r.UserId, ct)); if (p.Status != "Paid" || p.Requests.Any(x => x.Code == null)) throw new PoolException("ووچرها آماده دریافت نیستند", 409, "VOUCHERS_UNAVAILABLE"); return await renderer.RenderAsync(p, ct); }
}
