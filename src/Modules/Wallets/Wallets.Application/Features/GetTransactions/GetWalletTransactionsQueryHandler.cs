using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Features.GetTransactions;
using Wallets.Application.Contracts.Repositories;

namespace Wallets.Application.Features.GetTransactions;

public class GetWalletTransactionsQueryHandler : IRequestHandler<GetWalletTransactionsQuery, IReadOnlyList<GetTransactionsResponse>?>
{
    private readonly IWalletReadRepository _repo;

    public GetWalletTransactionsQueryHandler(IWalletReadRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<GetTransactionsResponse>?> Handle(GetWalletTransactionsQuery request, CancellationToken cancellationToken)
    {
        return await _repo.GetWalletTransactionsAsync(request.WalletId, request.Take);
    }
}
