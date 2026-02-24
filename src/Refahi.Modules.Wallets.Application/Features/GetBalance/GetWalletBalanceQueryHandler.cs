using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.GetBalance;

public class GetWalletBalanceQueryHandler : IRequestHandler<GetWalletBalanceQuery, WalletBalanceResponse?>
{
    private readonly IWalletReadRepository _repo;

    public GetWalletBalanceQueryHandler(IWalletReadRepository repo)
    {
        _repo = repo;
    }

    public async Task<WalletBalanceResponse?> Handle(GetWalletBalanceQuery request, CancellationToken cancellationToken)
    {
        return await _repo.GetWalletBalanceAsync(request.WalletId);
    }
}
