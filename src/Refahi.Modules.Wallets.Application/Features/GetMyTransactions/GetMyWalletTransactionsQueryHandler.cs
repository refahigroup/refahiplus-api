using MediatR;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Features.GetMyTransactions;

public sealed class GetMyWalletTransactionsQueryHandler
    : IRequestHandler<GetMyWalletTransactionsQuery, IReadOnlyList<MyWalletTransactionDto>>
{
    private readonly IWalletReadRepository _repo;

    public GetMyWalletTransactionsQueryHandler(IWalletReadRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<MyWalletTransactionDto>> Handle(
        GetMyWalletTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var take = request.Take is < 1 or > 100 ? 20 : request.Take;

        return await _repo.GetOwnerWalletTransactionsAsync(
            request.UserId,
            take,
            request.WalletType,
            request.OperationType,
            request.EntryType,
            cancellationToken);
    }
}
