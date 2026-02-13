using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wallets.Application.Contracts.Features.GetBalance;
using Wallets.Application.Contracts.Features.GetTransactions;

namespace Wallets.Application.Contracts.Repositories;

public interface IWalletReadRepository
{
    Task<WalletBalanceResponse> GetWalletBalanceAsync(Guid walletId);
    Task<IReadOnlyList<GetTransactionsResponse>> GetWalletTransactionsAsync(Guid walletId, int take);
}
