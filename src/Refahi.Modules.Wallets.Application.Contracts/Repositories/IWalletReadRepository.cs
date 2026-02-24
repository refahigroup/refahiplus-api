using Refahi.Modules.Wallets.Application.Contracts.Features.GetBalance;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Contracts.Repositories;

public interface IWalletReadRepository
{
    Task<WalletBalanceResponse> GetWalletBalanceAsync(Guid walletId);
    Task<IReadOnlyList<GetTransactionsResponse>> GetWalletTransactionsAsync(Guid walletId, int take);
}
