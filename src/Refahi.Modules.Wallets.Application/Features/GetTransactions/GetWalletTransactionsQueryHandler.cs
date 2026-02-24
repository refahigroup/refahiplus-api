using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.GetTransactions;

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
