using MediatR;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Features.GetBalance;
using Wallets.Application.Contracts.Repositories;

namespace Wallets.Application.Features.GetBalance;

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
